using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Save
{
    public enum SaveLoadFailure { None, NoFile, Corrupt, FutureVersion }
    public enum SaveRecoverySource { None, Primary, Temporary, Backup }

    public static class SaveRecoveryPolicy
    {
        public static SaveRecoverySource Choose(SaveData primary, SaveData temporary, SaveData backup)
        {
            SaveRecoverySource source = SaveRecoverySource.None;
            long latest = long.MinValue;
            if (backup != null) { source = SaveRecoverySource.Backup; latest = backup.savedAtUtcTicks; }
            if (temporary != null && temporary.savedAtUtcTicks >= latest)
            { source = SaveRecoverySource.Temporary; latest = temporary.savedAtUtcTicks; }
            // 같은 시각이면 정식 교체가 끝난 주 파일을 우선한다.
            if (primary != null && primary.savedAtUtcTicks >= latest) source = SaveRecoverySource.Primary;
            return source;
        }
    }

    public static class SaveValuePolicy
    {
        public static int SaturatingAdd(int current, int delta)
        {
            long total = (long)Math.Max(0, current) + delta;
            return (int)Math.Max(0L, Math.Min(int.MaxValue, total));
        }
    }

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
        public bool LastLoadUsedTemporary { get; private set; }
        public string LastMigrationReport { get; private set; }
        public SaveLoadFailure LastLoadFailure { get; private set; }

        [SerializeField] private int slot = 0;
        [SerializeField] private bool autoSaveOnPause = true;
        [SerializeField] private bool prettyPrint = true; // 개발 중 JSON 확인용. 출시 때 false

        /// <summary>저장 직전 (플레이어 좌표 등 런타임 값을 SaveData에 써넣을 기회)</summary>
        public static event Action BeforeSave;
        /// <summary>새 게임/회차 시작 또는 불러오기로 현재 세이브 객체가 교체된 직후.</summary>
        public static event Action CurrentChanged;
        public event Action OnSaved;
        public event Action OnSaveFailed;
        public event Action OnLoaded;
        public static event Action PartyChanged;
        public static event Action InventoryChanged;
        public static event Action EconomyChanged;

        private SaveData _current;
        private bool _suppressSaveWrites;

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
            if (paused && autoSaveOnPause && _current != null) SaveForSuspend();
        }

        private void OnApplicationQuit()
        {
            if (_current != null) SaveForSuspend();
        }

        #endregion

        // ────────────────────────────────────────────
        #region Save / Load

        public bool HasSave() => File.Exists(SavePath) || File.Exists(TempPath) || File.Exists(BackupPath);

        /// <summary>새 게임: 스타팅 그림자 1마리 + 기본 도구 지급</summary>
        public SaveData NewGame(ShadowData starter, int starterLevel = 10)
        {
            return CreateGame(starter, starterLevel, 1, 0, null);
        }

        /// <summary>엔딩 도감과 완료 횟수만 계승하고 진행/파티/선택 플래그는 초기화한다.</summary>
        public SaveData NewCycle(ShadowData starter, int starterLevel = 10)
        {
            if (_current == null || !_current.cycleCompleted || starter == null) return null;
            int nextCycle = Mathf.Max(1, _current.cycle) + 1;
            int completed = Mathf.Max(1, _current.completedCycles);
            var endings = new List<string>(_current.unlockedEndingIds ?? new List<string>());
            var history = new List<EndingRecordData>(_current.endingHistory ?? new List<EndingRecordData>());
            int fragments = Mathf.Max(0, _current.memoryFragments);
            SaveData next = CreateGame(starter, starterLevel, nextCycle, completed, endings);
            next.endingHistory = history;
            next.memoryFragments = fragments;
            SetFlag("ending_archive_count", endings.Count);
            SetFlag("new_cycle_memory", nextCycle);
            return next;
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
            _current.companionInstanceId = inst.instanceId;
            MarkRecorded(starter.shadowId);
            CurrentChanged?.Invoke();
            return _current;
        }

        public void Save(bool captureRuntimeState = true) => TrySave(captureRuntimeState);

        /// <summary>커튼 뒤 명령 전에 현재 슬롯을 별도 보존한다. 일반 .bak 순환과 독립적이다.</summary>
        public bool TryCreateDeveloperBackup(out string path)
        {
            path = string.Empty;
            if (_current == null || !TrySave()) return false;
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "backstage-backups");
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, $"save_{slot}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.json");
                File.Copy(SavePath, path, false);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { path = string.Empty; return false; }
        }

        /// <summary>실제 파일 교체가 완료됐는지 반환한다. 실패 시 현재 플레이 데이터는 유지한다.</summary>
        public bool TrySave(bool captureRuntimeState = true)
        {
            if (_current == null || _suppressSaveWrites) return false;
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
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 저장 실패: {e}");
                NotifySaveListeners(OnSaveFailed);
                return false;
            }
            // HUD 구독자의 예외가 파일 저장 성공을 실패로 바꾸지 않도록 분리한다.
            NotifySaveListeners(OnSaved);
            return true;
        }

        private static void NotifySaveListeners(Action notification)
        {
            if (notification == null) return;
            foreach (Action listener in notification.GetInvocationList())
            {
                try { listener(); }
                catch (Exception e) { Debug.LogWarning($"[SaveManager] 저장 알림 처리 실패: {e.Message}"); }
            }
        }

        /// <summary>
        /// OS 백그라운드·저메모리·종료 시 사용하는 안전 저장.
        /// 전투 도중에는 HP만 반영되고 소비품/보상이 아직 정산되지 않은 상태이므로,
        /// 전투 직전에 만든 체크포인트 파일을 보존한다.
        /// </summary>
        public void SaveForSuspend()
        {
            if (_current == null) return;
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return;
            if (GameFlowController.Instance == null && BattleManager.Instance != null &&
                BattleManager.Instance.Context != null && BattleManager.Instance.State != BattleState.None) return;
            Save();
        }

        public bool Load()
        {
            LastLoadUsedBackup = false;
            LastLoadUsedTemporary = false;
            LastLoadFailure = SaveLoadFailure.None;
            if (!HasSave())
            {
                LastLoadFailure = SaveLoadFailure.NoFile;
                return false;
            }
            SaveData primary = TryRead(SavePath, out bool futureVersion);
            if (futureVersion)
            {
                LastLoadFailure = SaveLoadFailure.FutureVersion;
                return false;
            }
            SaveData temporary = TryRead(TempPath, out futureVersion);
            if (futureVersion)
            {
                LastLoadFailure = SaveLoadFailure.FutureVersion;
                return false;
            }
            SaveData backup = TryRead(BackupPath, out futureVersion);
            if (futureVersion)
            {
                LastLoadFailure = SaveLoadFailure.FutureVersion;
                return false;
            }
            SaveRecoverySource recovery = SaveRecoveryPolicy.Choose(primary, temporary, backup);
            SaveData data = recovery == SaveRecoverySource.Primary ? primary
                : recovery == SaveRecoverySource.Temporary ? temporary
                : recovery == SaveRecoverySource.Backup ? backup : null;
            LastLoadUsedTemporary = recovery == SaveRecoverySource.Temporary;
            LastLoadUsedBackup = recovery == SaveRecoverySource.Backup;
            if (data == null)
            {
                LastLoadFailure = SaveLoadFailure.Corrupt;
                return false;
            }

            int sourceVersion = data.version;
            Migrate(data, out string migrationReport);
            LastMigrationReport = migrationReport;
            if (LastLoadUsedTemporary) LastMigrationReport += " · 중단된 임시 저장 복구";
            else if (LastLoadUsedBackup) LastMigrationReport += " · 백업 저장 복구";
            int removedUnknownShadows = RepairUnknownShadowReferences(data);
            foreach (var s in data.party) s.EnsureHp();
            foreach (var s in data.storage) s.EnsureHp();
            int restoredShadows = RepairRecordedOwnership(data);

            bool recovered = LastLoadUsedBackup || LastLoadUsedTemporary;
            bool canRewriteRecoveredSave = true;
            if (recovered && File.Exists(SavePath))
            {
                string state = primary != null ? "superseded" : "corrupt";
                string corruptPath = SavePath + $".{state}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
                try { File.Move(SavePath, corruptPath); }
                catch (Exception e)
                {
                    canRewriteRecoveredSave = false;
                    Debug.LogWarning($"[SaveManager] 손상 파일 격리 실패, 자동 재저장 보류: {e.Message}");
                }
            }

            _suppressSaveWrites = recovered;
            try
            {
                _current = data;
                RegionProgress.SyncCurrentMap();
                CurrentChanged?.Invoke();
                OnLoaded?.Invoke();
            }
            finally
            {
                _suppressSaveWrites = false;
            }
            if (restoredShadows > 0)
            {
                LastMigrationReport += $" · 누락 그림자 {restoredShadows}종 복구";
                Debug.LogWarning($"[SaveManager] 도감에는 기록됐지만 보유 목록에서 누락된 그림자 {restoredShadows}종을 파티/각본 서고에 복구했습니다.");
            }
            if (removedUnknownShadows > 0)
            {
                LastMigrationReport += $" · 알 수 없는 그림자 {removedUnknownShadows}개 정리";
                Debug.LogWarning($"[SaveManager] 현재 데이터베이스에 없는 그림자 {removedUnknownShadows}개를 세이브에서 격리했습니다.");
            }
            bool needsRewrite = sourceVersion < SaveData.CurrentVersion || LastLoadUsedBackup || LastLoadUsedTemporary ||
                                restoredShadows > 0 || removedUnknownShadows > 0;
            if (needsRewrite && canRewriteRecoveredSave) Save(false);
            return true;
        }

        public void DeleteSave()
        {
            foreach (var p in new[] { SavePath, BackupPath, TempPath })
                if (File.Exists(p)) File.Delete(p);
            _current = null;
            CurrentChanged?.Invoke();
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
            data.endingHistory ??= new List<EndingRecordData>();
            data.unlockedRegionIds ??= new List<string>();
            data.visitedRegionIds ??= new List<string>();
            data.companionBonds ??= new List<CompanionBondProgressData>();
            data.townRestorations ??= new List<TownRestorationProgressData>();
            data.fieldIncidentRecords ??= new List<FieldIncidentRecordData>();
            data.cycle = Mathf.Max(1, data.cycle);
            Sanitize(data);
            if (sourceVersion < 8)
                QuestProgressRecovery.InitializeCumulativeBaselines(data, QuestRepository.All);
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
            data.memoryFragments = Mathf.Max(0, data.memoryFragments);
            data.wildVictoryCount = Mathf.Max(0, data.wildVictoryCount);
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
            ShadowInstance starter = data.party.Concat(data.storage)
                .FirstOrDefault(x=>x.shadowId==data.starterShadowId);
            if(starter!=null)
            {
                starter.EnsureHp();bool wasFainted=starter.IsFainted;starter.RaiseToLevel(10);
                if(wasFainted)starter.currentHp=0;
            }
            if (data.party.Count == 0) data.companionInstanceId = string.Empty;
            else if (string.IsNullOrEmpty(data.companionInstanceId) ||
                     !data.party.Exists(x => x.instanceId == data.companionInstanceId))
                data.companionInstanceId = data.party[0].instanceId;

            var mergedItems = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in data.inventory)
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId) && item.count > 0)
                    mergedItems[item.itemId] = mergedItems.TryGetValue(item.itemId, out int count)
                        ? SaveValuePolicy.SaturatingAdd(count, item.count) : item.count;
            data.inventory.Clear();
            foreach (var pair in mergedItems) data.inventory.Add(new ItemStack { itemId = pair.Key, count = pair.Value });

            var mergedFlags = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var flag in data.flags)
                if (flag != null && !string.IsNullOrWhiteSpace(flag.key)) mergedFlags[flag.key] = flag.value;
            data.flags.Clear();
            foreach (var pair in mergedFlags) data.flags.Add(new FlagEntry { key = pair.Key, value = pair.Value });

            var validInstances = new HashSet<string>(data.party.Concat(data.storage).Select(x => x.instanceId), StringComparer.Ordinal);
            var bonds = new Dictionary<string, CompanionBondProgressData>(StringComparer.Ordinal);
            foreach (var bond in data.companionBonds)
            {
                if (bond == null || string.IsNullOrWhiteSpace(bond.instanceId) || !validInstances.Contains(bond.instanceId)) continue;
                if (!bonds.TryGetValue(bond.instanceId, out CompanionBondProgressData merged))
                    bonds.Add(bond.instanceId, new CompanionBondProgressData { instanceId = bond.instanceId });
                else merged.jointSkillUses = SaveValuePolicy.SaturatingAdd(merged.jointSkillUses, Mathf.Max(0, bond.jointSkillUses));
                bonds[bond.instanceId].experience = Mathf.Max(bonds[bond.instanceId].experience, Mathf.Max(0, bond.experience));
                bonds[bond.instanceId].jointSkillUses = Mathf.Max(bonds[bond.instanceId].jointSkillUses, Mathf.Max(0, bond.jointSkillUses));
            }
            data.companionBonds = bonds.Values.ToList();

            var towns = new Dictionary<string, TownRestorationProgressData>(StringComparer.Ordinal);
            foreach (var town in data.townRestorations)
            {
                if (town == null || string.IsNullOrWhiteSpace(town.regionId)) continue;
                if (!towns.TryGetValue(town.regionId, out TownRestorationProgressData merged))
                    towns.Add(town.regionId, new TownRestorationProgressData { regionId = town.regionId });
                towns[town.regionId].points = Mathf.Max(towns[town.regionId].points, Mathf.Max(0, town.points));
                towns[town.regionId].returnVisitClaimed |= town.returnVisitClaimed;
            }
            data.townRestorations = towns.Values.ToList();

            var incidents = new Dictionary<string, FieldIncidentRecordData>(StringComparer.Ordinal);
            foreach (var record in data.fieldIncidentRecords)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.incidentId)) continue;
                if(record.stage==0&&record.completionCount>0&&record.lastCompletedDay>=0){record.stage=4;record.activeDay=Mathf.Max(record.activeDay,record.lastCompletedDay);}
                if (!incidents.TryGetValue(record.incidentId, out FieldIncidentRecordData merged))
                    incidents.Add(record.incidentId, new FieldIncidentRecordData { incidentId = record.incidentId, lastCompletedDay = -1 });
                incidents[record.incidentId].lastCompletedDay = Mathf.Max(incidents[record.incidentId].lastCompletedDay, record.lastCompletedDay);
                incidents[record.incidentId].completionCount = Mathf.Max(incidents[record.incidentId].completionCount, Mathf.Max(0, record.completionCount));
                incidents[record.incidentId].encounterCleared|=record.encounterCleared;
                if(record.activeDay>=incidents[record.incidentId].activeDay){incidents[record.incidentId].activeDay=record.activeDay;incidents[record.incidentId].stage=Mathf.Clamp(record.stage,0,4);incidents[record.incidentId].regionId=record.regionId;incidents[record.incidentId].choiceId=record.choiceId;}
            }
            data.fieldIncidentRecords = incidents.Values.ToList();
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
                    int required = ShadowInstance.ExperienceToNext(shadow.level);
                    if (shadow.exp < required) break;
                    shadow.exp -= required;
                    shadow.level++;
                }
                if (shadow.level >= ShadowInstance.MaxLevel) shadow.exp = 0;
                if (!Enum.IsDefined(typeof(MemoryStage), shadow.memoryStage)) shadow.memoryStage = MemoryStage.Echo;
                if (!Enum.IsDefined(typeof(AwakeningPath), shadow.awakeningPath)) shadow.awakeningPath = AwakeningPath.None;
                if (!Enum.IsDefined(typeof(TrainingFocus), shadow.trainingFocus)) shadow.trainingFocus = TrainingFocus.Balanced;
                shadow.masteryPoints = Mathf.Max(0, shadow.masteryPoints);
                shadow.victories = Mathf.Max(0, shadow.victories);
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
        public static bool AddCapturedShadow(ShadowInstance shadow, out bool newlyRecorded)
        {
            return AddCapturedShadow(shadow, out newlyRecorded, out _);
        }

        public static bool AddCapturedShadow(ShadowInstance shadow, out bool newlyRecorded, out int duplicateFragments)
        {
            newlyRecorded = false;
            duplicateFragments = 0;
            if (Current == null || shadow == null || string.IsNullOrWhiteSpace(shadow.shadowId)) return false;
            if (string.IsNullOrEmpty(shadow.instanceId) || ContainsInstanceId(shadow.instanceId))
                shadow.instanceId = Guid.NewGuid().ToString("N");
            shadow.EnsureHp();
            newlyRecorded = MarkRecorded(shadow.shadowId);

            if (!newlyRecorded)
            {
                duplicateFragments = CollectionGrowthService.DuplicateFragments(shadow.Data);
                Current.memoryFragments = SaveValuePolicy.SaturatingAdd(Current.memoryFragments, duplicateFragments);
                InventoryChanged?.Invoke();
                return false;
            }

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

        /// <summary>현재 콘텐츠에 없는 개체 ID를 제거하고 전투 가능한 파티 슬롯을 복구한다.</summary>
        private static int RepairUnknownShadowReferences(SaveData data)
        {
            ShadowDatabase database = ShadowDatabase.Instance;
            if (database == null) return 0;
            int before = data.party.Count + data.storage.Count;
            data.party.RemoveAll(x => x == null || database.GetShadow(x.shadowId) == null);
            data.storage.RemoveAll(x => x == null || database.GetShadow(x.shadowId) == null);
            int removed = before - data.party.Count - data.storage.Count;

            if (data.party.Count == 0 && data.storage.Count > 0)
            {
                data.party.Add(data.storage[0]);
                data.storage.RemoveAt(0);
            }
            if (data.party.Count == 0)
            {
                ShadowData fallback = database.GetShadow(data.starterShadowId)
                                      ?? database.GetShadow("knight")
                                      ?? database.shadows.Find(x => x != null);
                if (fallback != null)
                {
                    data.party.Add(new ShadowInstance(fallback, 5));
                    data.starterShadowId = fallback.shadowId;
                    if (!data.seenShadowIds.Contains(fallback.shadowId)) data.seenShadowIds.Add(fallback.shadowId);
                    if (!data.recordedShadowIds.Contains(fallback.shadowId)) data.recordedShadowIds.Add(fallback.shadowId);
                }
            }
            return Mathf.Max(0, removed);
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

        public static bool CanRestParty(int cost, out string message)
        {
            message = string.Empty;
            if (Current == null || Current.party.Count == 0) { message = L10n.Get("shop.no_party", "휴식할 파티가 없습니다."); return false; }
            bool needsRest = Current.party.Exists(x => x.currentHp < x.MaxHp);
            if (!needsRest) { message = L10n.Get("shop.party_full", "파티의 HP가 이미 가득합니다."); return false; }
            cost = Mathf.Max(0, cost);
            if (Current.gold < cost) { message = L10n.Get("shop.rest_gold_short", "휴식에 필요한 금화가 부족합니다."); return false; }
            return true;
        }

        public static bool TryRestParty(int cost, out string message)
        {
            if (!CanRestParty(cost, out message)) return false;
            cost = Mathf.Max(0, cost);
            Current.gold -= cost;
            HealParty();
            EconomyChanged?.Invoke();
            message = L10n.Format("shop.rest_done", "막간 휴식 완료 · 파티 전원 회복 (-{0:N0} 금화)", cost);
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
            if (Current.companionInstanceId == target.instanceId)
                Current.companionInstanceId = (Current.party.Find(x => !x.IsFainted) ?? Current.party[0]).instanceId;
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

        public static bool CanSwapPartyStorage(string partyId, string storageId)
        {
            if (Current == null || string.IsNullOrEmpty(partyId) || string.IsNullOrEmpty(storageId)) return false;
            var outgoing = Current.party.Find(x => x.instanceId == partyId);
            var incoming = Current.storage.Find(x => x.instanceId == storageId);
            return outgoing != null && incoming != null && (!incoming.IsFainted ||
                Current.party.Exists(x => x != outgoing && !x.IsFainted));
        }

        /// <summary>Swap two slots atomically, preserving at least one living party member.</summary>
        public static bool TrySwapPartyStorage(string partyId, string storageId, out string message)
        {
            if (!CanSwapPartyStorage(partyId, storageId))
            { message = L10n.Get("party.swap_invalid", "교체할 두 그림자를 선택하세요. 파티에는 전투 가능한 그림자가 한 명 이상 필요합니다."); return false; }
            int partyIndex = Current.party.FindIndex(x => x.instanceId == partyId);
            int storageIndex = Current.storage.FindIndex(x => x.instanceId == storageId);
            var outgoing = Current.party[partyIndex]; var incoming = Current.storage[storageIndex];
            Current.party[partyIndex] = incoming; Current.storage[storageIndex] = outgoing;
            if (Current.companionInstanceId == outgoing.instanceId)
                Current.companionInstanceId = !incoming.IsFainted ? incoming.instanceId : Current.party.Find(x => !x.IsFainted).instanceId;
            PartyChanged?.Invoke();
            message = L10n.Format("party.swap_done", "{0}을(를) 파티에 넣고 {1}을(를) 서고로 옮겼습니다.", L10n.Text(incoming.DisplayName), L10n.Text(outgoing.DisplayName));
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

        public static bool SetCompanion(string instanceId)
        {
            if (Current == null || string.IsNullOrEmpty(instanceId) ||
                !Current.party.Exists(x => x.instanceId == instanceId)) return false;
            Current.companionInstanceId = instanceId;
            PartyChanged?.Invoke();
            return true;
        }

        #endregion

        // ────────────────────────────────────────────
        #region Inventory

        public static int GetItemCount(string itemId) =>
            Current?.inventory.Find(x => x.itemId == itemId)?.count ?? 0;

        public static void AdvancePlayTime(float seconds)
        {
            if(Current==null||seconds<=0f)return;
            Current.playTimeSeconds=Mathf.Max(0f,Current.playTimeSeconds+seconds);
        }

        public static void AddGold(int amount)
        {
            if (Current == null || amount == 0) return;
            Current.gold = SaveValuePolicy.SaturatingAdd(Current.gold, amount);
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
                stack.count = SaveValuePolicy.SaturatingAdd(stack.count, amount);
                if (stack.count == 0) Current.inventory.Remove(stack);
            }
            InventoryChanged?.Invoke();
        }

        public static bool CanBuyItem(ItemData item, out string message)
        {
            message = string.Empty;
            if (Current == null || item == null || string.IsNullOrEmpty(item.itemId) || item.buyPrice < 0)
            { message = L10n.Get("shop.invalid_buy", "구매할 수 없는 도구입니다."); return false; }
            if (item.isKeyItem && GetItemCount(item.itemId) > 0)
            { message = L10n.Get("shop.key_owned", "이미 보유한 핵심 도구입니다."); return false; }
            if (GetItemCount(item.itemId) == int.MaxValue)
            { message = L10n.Get("shop.stack_full", "이 도구를 더 보관할 수 없습니다."); return false; }
            if (Current.gold < item.buyPrice) { message = L10n.Get("shop.gold_short", "금화가 부족합니다."); return false; }
            return true;
        }

        public static bool TryBuyItem(ItemData item, out string message)
        {
            if (!CanBuyItem(item, out message)) return false;
            var stack = Current.inventory.Find(x => x.itemId == item.itemId);
            if (stack == null) { stack = new ItemStack { itemId = item.itemId }; Current.inventory.Add(stack); }
            Current.gold -= item.buyPrice;
            stack.count++;
            InventoryChanged?.Invoke();
            EconomyChanged?.Invoke();
            message = L10n.Format("shop.buy_done", "{0}을(를) 구매했습니다.", L10n.Text(item.displayName));
            return true;
        }

        public static bool CanSellItem(ItemData item, out string message)
        {
            message = string.Empty;
            if (Current == null || item == null || string.IsNullOrEmpty(item.itemId) || GetItemCount(item.itemId) <= 0)
            { message = L10n.Get("shop.no_sell_item", "판매할 도구가 없습니다."); return false; }
            if (item.isKeyItem) { message = L10n.Get("shop.key_no_sell", "핵심 도구는 판매할 수 없습니다."); return false; }
            if (item.sellPrice <= 0) { message = L10n.Get("shop.unsellable", "판매할 수 없는 도구입니다."); return false; }
            if (Current.gold > int.MaxValue - item.sellPrice)
            { message = L10n.Get("shop.gold_full", "금화를 더 보관할 수 없습니다."); return false; }
            return true;
        }

        public static bool TrySellItem(ItemData item, out string message)
        {
            if (!CanSellItem(item, out message)) return false;
            var stack = Current.inventory.Find(x => x.itemId == item.itemId);
            stack.count--;
            if (stack.count == 0) Current.inventory.Remove(stack);
            Current.gold += item.sellPrice;
            InventoryChanged?.Invoke();
            EconomyChanged?.Invoke();
            message = L10n.Format("shop.sell_done", "{0}을(를) 판매했습니다.", L10n.Text(item.displayName));
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
            long bestWaste = long.MaxValue;
            foreach (var stack in Current.inventory)
            {
                if (stack.count <= 0) continue;
                ItemData item = ShadowDatabase.Instance != null ? ShadowDatabase.Instance.GetItem(stack.itemId) : null;
                if (item == null || !item.usableInField ||
                    (item.effectType != ItemEffectType.HealFlat && item.effectType != ItemEffectType.HealRatio)) continue;
                int amount = FieldHealingAmount(item, target);
                long waste = Math.Abs((long)amount - missing) + (amount < missing ? missing : 0);
                if (waste < bestWaste) { best = item; bestWaste = waste; }
            }
            if (best == null) { message = "사용할 수 있는 회복 도구가 없습니다."; return false; }
            return TryUseFieldItem(best, target, out message);
        }

        public static bool TryPreviewFieldItem(ItemData item, ShadowInstance target, out int restored, out int excess, out string message)
        {
            restored = excess = 0;
            message = string.Empty;
            if (Current == null || item == null || GetItemCount(item.itemId) <= 0)
            { message = "보유하지 않은 도구입니다."; return false; }
            if (item.effectType == ItemEffectType.TraversalTool)
            { message = L10n.Get("inventory.traversal_hint", "이동 경로에서 자동으로 사용하는 도구입니다."); return false; }
            if (!item.usableInField)
            { message = item.usableInBattle ? "이 도구는 전투 중에만 사용할 수 있습니다." : L10n.Get("inventory.passive_hint", "가방에서 직접 사용하는 도구가 아닙니다."); return false; }
            if (target == null || (!Current.party.Contains(target) && !Current.storage.Contains(target)))
            { message = "사용할 그림자를 선택하세요."; return false; }
            if (item.effectType == ItemEffectType.GainExperience)
            {
                if (target.IsMaxLevel) { message = "이미 최고 레벨입니다."; return false; }
                int experience = Mathf.Max(1,Mathf.RoundToInt(item.value));
                int projectedLevel = ProjectLevelAfterExperience(target,experience);
                restored = experience;
                excess = projectedLevel-target.level;
                message = projectedLevel>target.level
                    ? $"예상 경험치 +{experience} · Lv.{target.level} → Lv.{projectedLevel}"
                    : $"예상 경험치 +{experience} · 사용 후 다음 레벨까지 {Mathf.Max(0,target.ExpToNext-target.exp-experience)}";
                return true;
            }
            if (target.IsFainted)
            { message = "기절한 그림자는 막간 휴식으로 회복해야 합니다."; return false; }
            if (item.effectType != ItemEffectType.HealFlat && item.effectType != ItemEffectType.HealRatio)
            { message = "필드에서 사용할 수 없는 효과입니다."; return false; }
            if (target.currentHp >= target.MaxHp)
            { message = "이미 HP가 가득합니다."; return false; }
            int amount = FieldHealingAmount(item, target);
            restored = Mathf.Min(target.MaxHp - target.currentHp, amount);
            excess = amount - restored;
            message = excess > 0 ? L10n.Format("inventory.heal_preview_excess", "예상 회복 +{0} HP · 초과 {1}", restored, excess)
                : L10n.Format("inventory.heal_preview", "예상 회복 +{0} HP", restored);
            return restored > 0;
        }

        private static int FieldHealingAmount(ItemData item, ShadowInstance target)
        {
            int healed = item.effectType == ItemEffectType.HealRatio
                ? Mathf.Max(1, Mathf.RoundToInt(target.MaxHp * Mathf.Clamp01(item.value)))
                : Mathf.Max(1, Mathf.RoundToInt(item.value));
            healed = Mathf.CeilToInt(healed * CompanionBondService.CurrentPassive().fieldHealingMultiplier);
            RegionData region = RegionRepository.GetByScene(Current.mapId);
            healed = Mathf.Max(1, Mathf.RoundToInt(healed *
                FieldWorldState.FieldHealingMultiplier(FieldWorldState.Current(region))));
            healed = CompanionBondService.ModifyFieldHealing(
                CompanionBondService.CurrentCompanionShadowId(), healed);
            return Mathf.Max(1, healed);
        }

        private static int ProjectLevelAfterExperience(ShadowInstance target,int amount)
        {
            int level=target.level;long experience=(long)Mathf.Max(0,target.exp)+Mathf.Max(0,amount);
            while(level<ShadowInstance.MaxLevel)
            {
                int required=ShadowInstance.ExperienceToNext(level);
                if(experience<required)break;
                experience-=required;level++;
            }
            return level;
        }

        public static bool TryUseFieldItem(ItemData item, ShadowInstance target, out string message)
        {
            if (!TryPreviewFieldItem(item, target, out int restored, out _, out message)) return false;
            if(item.effectType==ItemEffectType.GainExperience)
            {
                int before=target.level;target.AddExp(restored);AddItem(item.itemId,-1);PartyChanged?.Invoke();
                message=target.level>before
                    ? $"{L10n.Text(item.displayName)} 사용 · 경험치 +{restored} · Lv.{before} → Lv.{target.level}"
                    : $"{L10n.Text(item.displayName)} 사용 · 경험치 +{restored}";
                BackstageEasterEgg.OnFieldItemUsed(item.itemId);
                return true;
            }
            target.currentHp += restored;
            AddItem(item.itemId, -1);
            PartyChanged?.Invoke();
            message = L10n.Format("inventory.heal_used", "{0} 사용 · HP {1} 회복", L10n.Text(item.displayName), restored);
            BackstageEasterEgg.OnFieldItemUsed(item.itemId);
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
