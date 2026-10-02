using System;
using System.Collections.Generic;
using ShadowTheater.Data;

namespace ShadowTheater.Save
{
    /// <summary>
    /// 세이브 파일 루트 (JsonUtility 직렬화).
    /// JsonUtility는 Dictionary를 지원하지 않으므로 key/value 리스트로 저장하고, 조회는 SaveManager에서 처리.
    /// 구조를 바꿀 때는 CurrentVersion을 올리고 SaveManager.Migrate()에 변환 코드를 추가할 것.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 6;
        public const int MaxPartySize = 6;

        public int version = CurrentVersion;
        public long savedAtUtcTicks;
        public float playTimeSeconds;

        // ── 플레이어 위치 ──
        public string mapId = "Prologue";
        public int tileX;
        public int tileY;
        public int facing;              // 0=Down 1=Left 2=Right 3=Up
        public string checkpointMapId = "Prologue";   // 패배 시 복귀 지점 (여관/극장)
        public int checkpointX;
        public int checkpointY;

        // ── 그림자 ──
        public string starterShadowId;
        public List<ShadowInstance> party = new List<ShadowInstance>();    // 전투 출전 (최대 6)
        public List<ShadowInstance> storage = new List<ShadowInstance>();  // 각본 서고 (보관함)

        // ── 각본집 (도감) ──
        public List<string> seenShadowIds = new List<string>();     // 조우만 함 (실루엣 공개)
        public List<string> recordedShadowIds = new List<string>(); // 기록 완료 (Lore 해금)

        // ── 진행도 ──
        public List<FlagEntry> flags = new List<FlagEntry>();
        public List<string> clearedEncounterIds = new List<string>(); // 보스/고정 심볼 처치 기록
        public List<QuestProgressData> quests = new List<QuestProgressData>();
        public List<string> unlockedRegionIds = new List<string>();
        public List<string> visitedRegionIds = new List<string>();
        public string lastEndingId;
        public List<string> unlockedEndingIds = new List<string>();
        public int cycle = 1;
        public int completedCycles;
        public bool cycleCompleted;

        // ── 재화 / 도구 ──
        public int gold;
        public List<ItemStack> inventory = new List<ItemStack>();

        // ── 옵션 ──
        public float battleSpeed = 1f;
        public bool autoBattle;
    }

    /// <summary>퀘스트/이벤트 플래그. int 하나로 bool(0/1)과 단계값(0,1,2...)을 같이 표현</summary>
    [Serializable]
    public class FlagEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class QuestProgressData
    {
        public string questId;
        public bool completed;
        public bool rewardClaimed;
        public List<QuestObjectiveProgressData> objectives = new List<QuestObjectiveProgressData>();
    }

    [Serializable]
    public class QuestObjectiveProgressData
    {
        public string objectiveId;
        public int current;
    }
}
