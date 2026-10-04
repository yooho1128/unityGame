using System;
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Data;
using ShadowTheater.Story;
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
        public bool LastLoadUsedBackup { get; private set; }
        public string LastMigrationReport { get; private set; }

        [SerializeField] private int slot = 0;
        [SerializeField] private bool autoSaveOnPause = true;
        [SerializeField] private bool prettyPrint = true; // 개발 중 JSON 확인용. 출시 때 false

        /// <summary>저장 직전 (플레이어 좌표 등 런타임 값을 SaveData에 써넣을 기회)</summary>
        public static event Action BeforeSave;
        public event Action OnSaved;
        public event Action OnLoaded;
        public static event Action PartyChanged;
        public static event Action InventoryChanged;
        public static event Action EconomyChanged;

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
            return CreateGame(starter, starterLevel, 1, 0, null);
        }

        /// <summary>엔딩 도감과 완료 횟수만 계승하고 진행/파티/선택 플래그는 초기화한다.</summary>
        public SaveData NewCycle(ShadowData starter, int starterLevel = 5)
        {
            if (_current == null || !_current.cycleCompleted || starter == null) return null;
            int nextCycle = Mathf.Max(1, _current.cycle) + 1;
            int completed = Mathf.Max(1, _current.completedCycles);
            var endings = new List<string>(_current.unlockedEndingIds ?? new List<string>());
            return CreateGame(starter, starterLevel, nextCycle, completed, endings);
        }

        public bool CanStartNewCycle => _current != null && _current.cycleCompleted;

        private SaveData CreateGame(ShadowData starter, int starterLevel, int cycle, int completedCycles,
                                    List<string> inheritedEndings)
        {
            _current = new SaveData
            {
                saveGuid = Guid.NewGuid().ToString("N"),
                mapId = "PrologueTheater",
                checkpointMapId = "PrologueTheater",
                lastStableMapId = "PrologueTheater",
                tileX = 0,
                tileY = -5,
                checkpointX = 0,
                checkpointY = -5,
                starterShadowId = starter.shadowId,
                gold = 100,
                cycle = cycle,
                completedCycles = completedCycles,
                unlockedEndingIds = inheritedEndings ?? new List<string>()
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
                if (!string.IsNullOrEmpty(_current.mapId)) _current.lastStableMapId = _current.mapId;
                if (string.IsNullOrEmpty(_current.saveGuid)) _current.saveGuid = Guid.NewGuid().ToString("N");
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
            LastLoadUsedBackup = false;
            var data = TryRead(SavePath, out bool futureVersion);
            if (futureVersion) return false;
            if (data == null)
            {
                data = TryRead(BackupPath, out futureVersion);
                if (futureVersion) return false;
                LastLoadUsedBackup = data != null;
            }
            if (data == null) return false;

            int sourceVersion = data.version;
            Migrate(data, out string migrationReport);
            LastMigrationReport = migrationReport;
            foreach (var s in data.party) s.EnsureHp();
            foreach (var s in data.storage) s.EnsureHp();
            int restoredShadows = RepairRecordedOwnership(data);

            _current = data;
            RegionProgress.SyncCurrentMap();
            OnLoaded?.Invoke();
            if (LastLoadUsedBackup && File.Exists(SavePath))
            {
                string corruptPath = SavePath + $".corrupt_{DateTime.UtcNow:yyyyMMddHHmmss}";
                try { File.Move(SavePath, corruptPath); }
                catch (Exception e) { Debug.LogWarning($"[SaveManager] 손상 파일 격리 실패: {e.Message}"); }
            }
            if (restoredShadows > 0)
            {
                LastMigrationReport += $" · 누락 그림자 {restoredShadows}종 복구";
                Debug.LogWarning($"[SaveManager] 도감에는 기록됐지만 보유 목록에서 누락된 그림자 {restoredShadows}종을 파티/각본 서고에 복구했습니다.");
            }
            if (sourceVersion < SaveData.CurrentVersion || LastLoadUsedBackup || restoredShadows > 0) Save(false);
            return true;
        }

        public void DeleteSave()
        {
            foreach (var p in new[] { SavePath, BackupPath, TempPath })
                if (File.Exists(p)) File.Delete(p);
            _current = null;
        }

        private static SaveData TryRead(string path, out bool futureVersion)
        {
            futureVersion = false;
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.party == null) return null;
                if (data.version > SaveData.CurrentVersion)
                {
                    futureVersion = true;
                    Debug.LogWarning($"[SaveManager] 더 새로운 세이브 버전입니다: {data.version}");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] 읽기 실패 ({path}): {e.Message}");
                return null;
            }
        }

        /// <summary>구버전 세이브 변환. 버전 올릴 때마다 case 추가</summary>
        public static bool Migrate(SaveData data, out string report)
        {
            if (data == null) { report = "세이브 데이터 없음"; return false; }
            int sourceVersion = data.version;
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
            if (data.version < 5)
            {
                data.cycle = Mathf.Max(1, data.cycle);
                data.cycleCompleted = !string.IsNullOrEmpty(data.lastEndingId);
                data.completedCycles = data.cycleCompleted ? Mathf.Max(1, data.completedCycles) : 0;
            }
            if (data.version < 6)
            {
                data.unlockedRegionIds = new List<string>();
                data.visitedRegionIds = new List<string>();
            }
            if (data.version < 7)
            {
                if (string.IsNullOrEmpty(data.saveGuid)) data.saveGuid = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(data.mapId) || data.mapId == "Prologue")
                {
                    data.mapId = "PrologueTheater";
                    data.tileX = 0;
                    data.tileY = -5;
                }
                if (string.IsNullOrEmpty(data.checkpointMapId) || data.checkpointMapId == "Prologue")
                {
                    data.checkpointMapId = "PrologueTheater";
                    data.checkpointX = 0;
                    data.checkpointY = -5;
                }
                data.lastStableMapId = data.mapId;
            }

            data.flags ??= new List<FlagEntry>();
            data.clearedEncounterIds ??= new List<string>();
            data.seenShadowIds ??= new List<string>();
            data.recordedShadowIds ??= new List<string>();
            data.inventory ??= new List<ItemStack>();
            data.unlockedEndingIds ??= new List<string>();
            data.unlockedRegionIds ??= new List<string>();
            data.visitedRegionIds ??= new List<string>();
            data.cycle = Mathf.Max(1, data.cycle);
            Sanitize(data);
            data.version = SaveData.CurrentVersion;
            report = sourceVersion == SaveData.CurrentVersion
                ? $"v{SaveData.CurrentVersion} 무결성 정리 완료"
                : $"v{sourceVersion} → v{SaveData.CurrentVersion} 마이그레이션 완료";
            return sourceVersion != SaveData.CurrentVersion;
        }

        private static void Sanitize(SaveData data)
        {
            data.saveGuid = string.IsNullOrEmpty(data.saveGuid) ? Guid.NewGuid().ToString("N") : data.saveGuid;
            data.gold = Mathf.Max(0, data.gold);
            data.playTimeSeconds = Mathf.Max(0f, data.playTimeSeconds);
            data.battleSpeed = Mathf.Clamp(data.battleSpeed <= 0f ? 1f : data.battleSpeed, 1f, 3f);
            data.facing = Mathf.Clamp(data.facing, 0, 3);
            data.mapId = string.IsNullOrEmpty(data.mapId) ? "PrologueTheater" : data.mapId;
            data.checkpointMapId = string.IsNullOrEmpty(data.checkpointMapId) ? data.mapId : data.checkpointMapId;
            data.lastStableMapId = string.IsNullOrEmpty(data.lastStableMapId) ? data.mapId : data.lastStableMapId;

            CleanIds(data.seenShadowIds);
            CleanIds(data.recordedShadowIds);
            CleanIds(data.clearedEncounterIds);
            CleanIds(data.unlockedRegionIds);
            CleanIds(data.visitedRegionIds);
            CleanIds(data.unlockedEndingIds);
            foreach (string id in data.recordedShadowIds)
                if (!data.seenShadowIds.Contains(id)) data.seenShadowIds.Add(id);

            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            CleanShadows(data.party, instanceIds);
            CleanShadows(data.storage, instanceIds);
            while (data.party.Count > SaveData.MaxPartySize)
            {
                int last = data.party.Count - 1;
                data.storage.Insert(0, data.party[last]);
                data.party.RemoveAt(last);
            }
            if (string.IsNullOrEmpty(data.starterShadowId) && data.party.Count > 0)
                data.starterShadowId = data.party[0].shadowId;

            var mergedItems = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in data.inventory)
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId) && item.count > 0)
                    mergedItems[item.itemId] = mergedItems.TryGetValue(item.itemId, out int count) ? count + item.count : item.count;
            data.inventory.Clear();
            foreach (var pair in mergedItems) data.inventory.Add(new ItemStack { itemId = pair.Key, count = pair.Value });

            var mergedFlags = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var flag in data.flags)
                if (flag != null && !string.IsNullOrWhiteSpace(flag.key)) mergedFlags[flag.key] = flag.value;
            data.flags.Clear();
            foreach (var pair in mergedFlags) data.flags.Add(new FlagEntry { key = pair.Key, value = pair.Value });
        }

        private static void CleanIds(List<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            values.RemoveAll(x => string.IsNullOrWhiteSpace(x) || !seen.Add(x));
        }

        private static void CleanShadows(List<ShadowInstance> shadows, HashSet<string> instanceIds)
        {
            shadows.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.shadowId));
            foreach (var shadow in shadows)
            {
                shadow.level = Mathf.Clamp(shadow.level, 1, ShadowInstance.MaxLevel);
                shadow.exp = Mathf.Max(0, shadow.exp);
                while (shadow.level < ShadowInstance.MaxLevel)
                {
                    int required = 20 + shadow.level * shadow.level * 5;
                    if (shadow.exp < required) break;
                    shadow.exp -= required;
                    shadow.level++;
                }
                if (shadow.level >= ShadowInstance.MaxLevel) shadow.exp = 0;
                if (!Enum.IsDefined(typeof(MemoryStage), shadow.memoryStage)) shadow.memoryStage = MemoryStage.Echo;
                if (!Enum.IsDefined(typeof(AwakeningPath), shadow.awakeningPath)) shadow.awakeningPath = AwakeningPath.None;
                if (string.IsNullOrEmpty(shadow.instanceId) || !instanceIds.Add(shadow.instanceId))
                {
                    shadow.instanceId = Guid.NewGuid().ToString("N");
                    instanceIds.Add(shadow.instanceId);
                }
                NormalizeMemoryStage(shadow);
            }
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
            if (Current == null || shadow == null || string.IsNullOrWhiteSpace(shadow.shadowId)) return false;
            if (string.IsNullOrEmpty(shadow.instanceId) || ContainsInstanceId(shadow.instanceId))
                shadow.instanceId = Guid.NewGuid().ToString("N");
            shadow.EnsureHp();
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

        /// <summary>도감 기록은 남았지만 파티·서고에서 사라진 구버전 포획 개체를 복구한다.</summary>
        private static int RepairRecordedOwnership(SaveData data)
        {
            var ownedShadowIds = new HashSet<string>(StringComparer.Ordinal);
            int totalLevel = 0;
            int ownedCount = 0;
            foreach (var instance in data.party)
            {
                ownedShadowIds.Add(instance.shadowId);
                totalLevel += Mathf.Max(1, instance.level);
                ownedCount++;
            }
            foreach (var instance in data.storage)
            {
                ownedShadowIds.Add(instance.shadowId);
                totalLevel += Mathf.Max(1, instance.level);
                ownedCount++;
            }

            int recoveryLevel = ownedCount > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)totalLevel / ownedCount)) : 5;
            int restored = 0;
            foreach (string shadowId in data.recordedShadowIds)
            {
                if (ownedShadowIds.Contains(shadowId)) continue;
                ShadowData definition = ShadowDatabase.Instance != null ? ShadowDatabase.Instance.GetShadow(shadowId) : null;
                if (definition == null)
                {
                    Debug.LogWarning($"[SaveManager] 누락 그림자 데이터를 찾지 못해 복구 보류: {shadowId}");
                    continue;
                }
                var restoredInstance = new ShadowInstance(definition, recoveryLevel);
                if (data.party.Count < SaveData.MaxPartySize) data.party.Add(restoredInstance);
                else data.storage.Add(restoredInstance);
                ownedShadowIds.Add(shadowId);
                restored++;
            }
            return restored;
        }

        private static bool ContainsInstanceId(string instanceId) =>
            Current.party.Exists(x => x.instanceId == instanceId) || Current.storage.Exists(x => x.instanceId == instanceId);

        public static void HealParty()
        {
            if (Current == null) return;
            foreach (var s in Current.party) s.FullHeal();
            PartyChanged?.Invoke();
        }

        public static bool TryRestParty(int cost, out string message)
        {
            message = string.Empty;
            if (Current == null) { message = "휴식할 파티가 없습니다."; return false; }
            bool needsRest = Current.party.Exists(x => x.currentHp < x.MaxHp);
            if (!needsRest) { message = "파티의 HP가 이미 가득합니다."; return false; }
            cost = Mathf.Max(0, cost);
            if (Current.gold < cost) { message = "휴식에 필요한 금화가 부족합니다."; return false; }
            Current.gold -= cost;
            HealParty();
            EconomyChanged?.Invoke();
            message = $"막간 휴식 완료 · 파티 전원 회복 (-{cost} 금화)";
            return true;
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

        public static void AddGold(int amount)
        {
            if (Current == null || amount == 0) return;
            Current.gold = Mathf.Max(0, Current.gold + amount);
            EconomyChanged?.Invoke();
        }

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
            InventoryChanged?.Invoke();
        }

        public static bool TryBuyItem(ItemData item, out string message)
        {
            message = string.Empty;
            if (Current == null || item == null) { message = "구매할 도구가 없습니다."; return false; }
            int price = Mathf.Max(0, item.buyPrice);
            if (Current.gold < price) { message = "금화가 부족합니다."; return false; }
            Current.gold -= price;
            AddItem(item.itemId, 1);
            EconomyChanged?.Invoke();
            message = $"{item.displayName}을(를) 구매했습니다.";
            return true;
        }

        public static bool TrySellItem(ItemData item, out string message)
        {
            message = string.Empty;
            if (Current == null || item == null || GetItemCount(item.itemId) <= 0)
            { message = "판매할 도구가 없습니다."; return false; }
            AddItem(item.itemId, -1);
            Current.gold += Mathf.Max(0, item.sellPrice);
            EconomyChanged?.Invoke();
            message = $"{item.displayName}을(를) 판매했습니다.";
            return true;
        }

        /// <summary>선택한 그림자에게 낭비가 가장 적은 필드 회복 도구 하나를 사용한다.</summary>
        public static bool TryUseBestHealingItem(ShadowInstance target, out string message)
        {
            message = string.Empty;
            if (Current == null || target == null || (!Current.party.Contains(target) && !Current.storage.Contains(target)))
            { message = "회복할 그림자를 선택하세요."; return false; }
            if (target.IsFainted) { message = "기절한 그림자는 휴식 지점에서 회복해야 합니다."; return false; }
            int missing = target.MaxHp - target.currentHp;
            if (missing <= 0) { message = "이미 HP가 가득합니다."; return false; }

            ItemData best = null;
            int bestWaste = int.MaxValue;
            foreach (var stack in Current.inventory)
            {
                if (stack.count <= 0) continue;
                ItemData item = ShadowDatabase.Instance != null ? ShadowDatabase.Instance.GetItem(stack.itemId) : null;
                if (item == null || !item.usableInField ||
                    (item.effectType != ItemEffectType.HealFlat && item.effectType != ItemEffectType.HealRatio)) continue;
                int amount = item.effectType == ItemEffectType.HealRatio
                    ? Mathf.Max(1, Mathf.RoundToInt(target.MaxHp * Mathf.Clamp01(item.value)))
                    : Mathf.Max(1, Mathf.RoundToInt(item.value));
                int waste = Mathf.Abs(amount - missing) + (amount < missing ? missing : 0);
                if (waste < bestWaste) { best = item; bestWaste = waste; }
            }
            if (best == null) { message = "사용할 수 있는 회복 도구가 없습니다."; return false; }
            int healed = best.effectType == ItemEffectType.HealRatio
                ? Mathf.Max(1, Mathf.RoundToInt(target.MaxHp * Mathf.Clamp01(best.value)))
                : Mathf.Max(1, Mathf.RoundToInt(best.value));
            int before = target.currentHp;
            target.currentHp = Mathf.Min(target.MaxHp, target.currentHp + healed);
            AddItem(best.itemId, -1);
            PartyChanged?.Invoke();
            message = $"{best.displayName} 사용 · HP {target.currentHp - before} 회복";
            return true;
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
            InventoryChanged?.Invoke();
        }

        #endregion
    }
}
