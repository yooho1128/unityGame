using System;
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Save
{
    /// <summary>
    /// 세이브/로드 + 진행 상태 조회 창구. 게임 전체에서 SaveManager.Current로 현재 진행 데이터에 접근.
    ///
    /// - 저장 위치: Application.persistentDataPath/save_{slot}.json
    /// - 안전 저장: 임시 파일에 쓰고 교체, 직전 파일은 .bak로 보관 → 저장 중 앱 종료돼도 파일 안 깨짐
    /// - 모바일: 앱이 백그라운드로 갈 때(OnApplicationPause) 자동 저장
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        public static SaveData Current => Instance != null ? Instance._current : null;

        [SerializeField] private int slot = 0;
        [SerializeField] private bool autoSaveOnPause = true;
        [SerializeField] private bool prettyPrint = true; // 개발 중 JSON 확인용. 출시 때 false

        /// <summary>저장 직전 (플레이어 좌표 등 런타임 값을 SaveData에 써넣을 기회)</summary>
        public static event Action BeforeSave;
        public event Action OnSaved;
        public event Action OnLoaded;
        public static event Action PartyChanged;

        private SaveData _current;

        private string SavePath => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");
        private string BackupPath => SavePath + ".bak";
        private string TempPath => SavePath + ".tmp";

        // ────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_current != null) _current.playTimeSeconds += Time.unscaledDeltaTime;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && autoSaveOnPause && _current != null) Save();
        }

        private void OnApplicationQuit()
        {
            if (_current != null) Save();
        }

        #endregion

        // ────────────────────────────────────────────
        #region Save / Load

        public bool HasSave() => File.Exists(SavePath) || File.Exists(BackupPath);

        /// <summary>새 게임: 스타팅 그림자 1마리 + 기본 도구 지급</summary>
        public SaveData NewGame(ShadowData starter, int starterLevel = 5)
        {
            _current = new SaveData
            {
                starterShadowId = starter.shadowId,
                gold = 100
            };
            var inst = new ShadowInstance(starter, starterLevel);
            _current.party.Add(inst);
            MarkRecorded(starter.shadowId);
            return _current;
        }

        public void Save(bool captureRuntimeState = true)
        {
            if (_current == null) return;
            try
            {
                if (captureRuntimeState) BeforeSave?.Invoke();
                _current.version = SaveData.CurrentVersion;
                _current.savedAtUtcTicks = DateTime.UtcNow.Ticks;
                string json = JsonUtility.ToJson(_current, prettyPrint);

                File.WriteAllText(TempPath, json);
                if (File.Exists(SavePath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(SavePath, BackupPath);
                }
                File.Move(TempPath, SavePath);

                OnSaved?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 저장 실패: {e}");
            }
        }

        public bool Load()
        {
            var data = TryRead(SavePath) ?? TryRead(BackupPath);
            if (data == null) return false;

            Migrate(data);
            foreach (var s in data.party) s.EnsureHp();
            foreach (var s in data.storage) s.EnsureHp();

            _current = data;
            OnLoaded?.Invoke();
            return true;
        }

        public void DeleteSave()
        {
            foreach (var p in new[] { SavePath, BackupPath, TempPath })
                if (File.Exists(p)) File.Delete(p);
            _current = null;
        }

        private static SaveData TryRead(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data != null && data.party != null ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] 읽기 실패 ({path}): {e.Message}");
                return null;
            }
        }

        /// <summary>구버전 세이브 변환. 버전 올릴 때마다 case 추가</summary>
        private static void Migrate(SaveData data)
        {
            if (data.version < 2 || data.quests == null)
                data.quests = new List<QuestProgressData>();
            if (data.version < 3 || data.unlockedEndingIds == null)
                data.unlockedEndingIds = new List<string>();

            data.party ??= new List<ShadowInstance>();
            data.storage ??= new List<ShadowInstance>();
            if (data.version < 4)
            {
                foreach (var shadow in data.party) NormalizeMemoryStage(shadow);
                foreach (var shadow in data.storage) NormalizeMemoryStage(shadow);
            }

            data.flags ??= new List<FlagEntry>();
            data.clearedEncounterIds ??= new List<string>();
            data.seenShadowIds ??= new List<string>();
            data.recordedShadowIds ??= new List<string>();
            data.inventory ??= new List<ItemStack>();
            data.version = SaveData.CurrentVersion;
        }

        private static void NormalizeMemoryStage(ShadowInstance shadow)
        {
            if (shadow == null) return;
            if (shadow.memoryStage != MemoryStage.TrueName) shadow.awakeningPath = AwakeningPath.None;
            else if (shadow.awakeningPath == AwakeningPath.None) shadow.awakeningPath = AwakeningPath.Salvation;
        }

        #endregion

        // ────────────────────────────────────────────
        #region Flags (퀘스트/이벤트)

        public static int GetFlag(string key, int defaultValue = 0)
        {
            var f = Current?.flags.Find(x => x.key == key);
            return f != null ? f.value : defaultValue;
        }

        public static bool HasFlag(string key) => GetFlag(key) != 0;

        public static void SetFlag(string key, int value = 1)
        {
            if (Current == null) return;
            var f = Current.flags.Find(x => x.key == key);
            if (f != null) f.value = value;
            else Current.flags.Add(new FlagEntry { key = key, value = value });
        }

        public static bool IsEncounterCleared(string encounterId) =>
            !string.IsNullOrEmpty(encounterId) && Current != null && Current.clearedEncounterIds.Contains(encounterId);

        public static void MarkEncounterCleared(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId) || Current == null) return;
            if (!Current.clearedEncounterIds.Contains(encounterId)) Current.clearedEncounterIds.Add(encounterId);
        }

        #endregion

        // ────────────────────────────────────────────
        #region Shadows / 각본집

        public static void MarkSeen(string shadowId)
        {
            if (Current == null || string.IsNullOrEmpty(shadowId)) return;
            if (!Current.seenShadowIds.Contains(shadowId)) Current.seenShadowIds.Add(shadowId);
        }

        /// <summary>각본집 기록 (Lore 해금). 처음 기록이면 true</summary>
        public static bool MarkRecorded(string shadowId)
        {
            if (Current == null || string.IsNullOrEmpty(shadowId)) return false;
            MarkSeen(shadowId);
            if (Current.recordedShadowIds.Contains(shadowId)) return false;
            Current.recordedShadowIds.Add(shadowId);
            return true;
        }

        public static bool IsRecorded(string shadowId) =>
            Current != null && Current.recordedShadowIds.Contains(shadowId);

        /// <summary>포획한 그림자 추가. 파티가 가득 차면 서고(보관함)로. 파티에 들어갔으면 true</summary>
        public static bool AddCapturedShadow(ShadowInstance shadow)
        {
            if (Current == null || shadow == null) return false;
            MarkRecorded(shadow.shadowId);

            if (Current.party.Count < SaveData.MaxPartySize)
            {
                Current.party.Add(shadow);
                PartyChanged?.Invoke();
                return true;
            }
            Current.storage.Add(shadow);
            PartyChanged?.Invoke();
            return false;
        }

        public static void HealParty()
        {
            if (Current == null) return;
            foreach (var s in Current.party) s.FullHeal();
        }

        public static bool HasAliveShadow() =>
            Current != null && Current.party.Exists(s => !s.IsFainted);

        public static bool MoveToStorage(string instanceId)
        {
            if (Current == null || Current.party.Count <= 1) return false;
            int index = Current.party.FindIndex(x => x.instanceId == instanceId);
            if (index < 0) return false;
            var target = Current.party[index];
            if (!target.IsFainted && Current.party.FindAll(x => !x.IsFainted).Count <= 1) return false;
            Current.party.RemoveAt(index);
            Current.storage.Add(target);
            PartyChanged?.Invoke();
            return true;
        }

        public static bool MoveToParty(string instanceId)
        {
            if (Current == null || Current.party.Count >= SaveData.MaxPartySize) return false;
            int index = Current.storage.FindIndex(x => x.instanceId == instanceId);
            if (index < 0) return false;
            var target = Current.storage[index];
            Current.storage.RemoveAt(index);
            Current.party.Add(target);
            PartyChanged?.Invoke();
            return true;
        }

        public static bool MovePartySlot(string instanceId, int direction)
        {
            if (Current == null || direction == 0) return false;
            int from = Current.party.FindIndex(x => x.instanceId == instanceId);
            int to = from + Math.Sign(direction);
            if (from < 0 || to < 0 || to >= Current.party.Count) return false;
            var target = Current.party[from];
            Current.party.RemoveAt(from);
            Current.party.Insert(to, target);
            PartyChanged?.Invoke();
            return true;
        }

        #endregion

        // ────────────────────────────────────────────
        #region Inventory

        public static int GetItemCount(string itemId) =>
            Current?.inventory.Find(x => x.itemId == itemId)?.count ?? 0;

        public static void AddItem(string itemId, int amount = 1)
        {
            if (Current == null || amount == 0) return;
            var stack = Current.inventory.Find(x => x.itemId == itemId);
            if (stack == null)
            {
                if (amount < 0) return;
                Current.inventory.Add(new ItemStack { itemId = itemId, count = amount });
            }
            else
            {
                stack.count = Mathf.Max(0, stack.count + amount);
                if (stack.count == 0) Current.inventory.Remove(stack);
            }
        }

        /// <summary>BattleContext.inventory용 Dictionary로 변환</summary>
        public static Dictionary<ItemData, int> BuildBattleInventory()
        {
            var dict = new Dictionary<ItemData, int>();
            if (Current == null) return dict;
            foreach (var stack in Current.inventory)
            {
                var item = ShadowDatabase.Instance.GetItem(stack.itemId);
                if (item != null && item.usableInBattle && stack.count > 0) dict[item] = stack.count;
            }
            return dict;
        }

        /// <summary>전투 후 남은 수량을 세이브에 반영</summary>
        public static void ApplyBattleInventory(Dictionary<ItemData, int> battleInventory)
        {
            if (Current == null || battleInventory == null) return;
            foreach (var kv in battleInventory)
            {
                var stack = Current.inventory.Find(x => x.itemId == kv.Key.itemId);
                if (stack == null) continue;
                stack.count = kv.Value;
                if (stack.count <= 0) Current.inventory.Remove(stack);
            }
        }

        #endregion
    }
}
