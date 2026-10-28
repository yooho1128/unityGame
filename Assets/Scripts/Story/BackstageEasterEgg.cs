using System;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.Story
{
    /// <summary>The hidden stage manager is reached only through an in-world ritual, in release too.</summary>
    public static class BackstageEasterEgg
    {
        public const string UnlockedFlag = "easter_egg_backstage_unlocked";
        public const string UsedFlag = "developer_cheats_used";
        private static readonly BackstageRitual ritual = new BackstageRitual();
        private static SaveData save;
        private static bool hooked;
        private static int locationGeneration;
        private static readonly Color Gold = new Color(.85f, .71f, .43f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            hooked = false; save = null; ritual.Reset(); locationGeneration++;
        }
        private static void OnSceneChanged(Scene previous, Scene next)
        { ritual.Reset(); locationGeneration++; }

        private static bool AtShrine()
        {
            if (!ReferenceEquals(save, SaveManager.Current))
            { save = SaveManager.Current; ritual.Reset(); }
            var player = PlayerController.Instance;
            return save != null && SceneManager.GetActiveScene().name == "PrologueTheater" &&
                player != null && player.Cell == new Vector2Int(-3, 2) && player.Facing == FacingDir.Up &&
                !player.IsMoving && (MapLoader.Instance == null || !MapLoader.Instance.IsTransitioning) &&
                (GameFlowController.Instance == null || !GameFlowController.Instance.IsInBattle);
        }
        private static void EnsureHook()
        {
            if (hooked) return;
            hooked = true;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }
        public static void OnPlayerStep()
        {
            // Walking away between ritual actions invalidates it, even if the player returns later.
            ritual.Reset(); locationGeneration++;
        }
        public static void OnFieldItemUsed(string itemId)
        {
            EnsureHook();
            if (!AtShrine()) { ritual.Reset(); return; }
            if (!SaveManager.HasFlag(UnlockedFlag)) ritual.Use(itemId);
        }
        public static Action CompletedReading(string dialogueId)
        {
            EnsureHook();
            if (dialogueId != BackstageRitual.ShrineId || !AtShrine())
            { ritual.Reset(); return null; }
            int generation = locationGeneration;
            var originalSave = SaveManager.Current;
            return () =>
            {
                if (generation == locationGeneration && ReferenceEquals(originalSave, SaveManager.Current) && AtShrine())
                    ritual.Read();
                else ritual.Reset();
            };
        }
        public static bool TryOpen(string dialogueId)
        {
            EnsureHook();
            if (dialogueId != BackstageRitual.ShrineId || !AtShrine()) return false;
            if (SaveManager.HasFlag(UnlockedFlag)) return ShowMain();
            if (!ritual.Ready) return false;
            return Show("무대 뒤의 목소리", "세 번 읽고, 세 번 연습한 자여. 마지막 대사의 주인은 누구인가?",
                new[] { "나는 이 무대의 연출가다.", "아직 내 대사가 아니다." }, choice =>
                {
                    if (choice != 0 || !AtShrine()) { ritual.Reset(); return; }
                    SaveManager.SetFlag(UnlockedFlag);
                    SaveManager.Instance?.Save();
                    ritual.Reset();
                    ShowMain();
                });
        }
        private static bool ShowMain() => Show("장막 뒤 소품지기", "돌아오셨군요, 연출가님. 오늘은 어떤 장면을 고칠까요?",
            new[] { "소품 창고를 연다.", "배우들을 정비한다.", "장막을 닫는다." }, choice =>
            {
                if (choice == 0) ShowSupplies();
                if (choice == 1) ShowActors();
            });
        private static void ShowSupplies() => Show("소품지기", "이 장면은 본래 각본에 없었습니다. 필요한 소품을 고르세요.",
            new[] { "소품비 10,000 금화를 받는다.", "일반 소모품을 각각 10개 받는다.", "돌아간다." }, choice =>
            { if (choice < 2) Execute(choice); else ShowMain(); });
        private static void ShowActors() => Show("무대 감독", "막을 올리기 전에, 배우들에게 숨을 돌릴 시간을.",
            new[] { "파티 전원을 회복한다.", "파티 전원 5레벨을 올리고 회복한다.", "돌아간다." }, choice =>
            { if (choice < 2) Execute(choice + 2); else ShowMain(); });
        private static bool Show(string speaker, string text, string[] choices, Action<int> action)
        {
            var controller = DialogueController.Instance;
            if (controller == null) return false;
            var sequence = new DialogueSequence { dialogueId = "backstage_dynamic" };
            sequence.lines.Add(new DialogueLine { speaker = speaker, text = text, emotion = "mystery" });
            foreach (var choice in choices) sequence.choices.Add(new DialogueChoice { text = choice });
            int selected = -1;
            var originalSave = SaveManager.Current;
            return controller.PlayDynamic(sequence, Gold, i => selected = i, () =>
            {
                if (selected >= 0 && ReferenceEquals(originalSave, SaveManager.Current) && AtShrine())
                    action?.Invoke(selected);
            });
        }
        private static void Execute(int action)
        {
            if (!AtShrine() || !SaveManager.HasFlag(UnlockedFlag) || SaveManager.Instance == null) return;
            if (action == 1 && ShadowDatabase.Instance == null) return;
            if (!SaveManager.Instance.TryCreateDeveloperBackup(out _))
            {
                Show("무대 감독", "기록을 복사하지 못했습니다. 원래 각본을 지키기 위해 장면을 바꾸지 않았습니다.",
                    new[] { "돌아간다." }, _ => ShowMain()); return;
            }
            if (action == 0) SaveManager.AddGold(10000);
            else if (action == 1)
                foreach (var item in ShadowDatabase.Instance.items.Where(i => i != null && !i.isKeyItem &&
                    !i.isEventOnly && (i.usableInBattle || i.usableInField)))
                    SaveManager.AddItem(item.itemId, 10);
            else if (action == 2) SaveManager.HealParty();
            else if (action == 3)
            {
                foreach (var member in SaveManager.Current.party)
                    member.RaiseToLevel(Mathf.Min(ShadowInstance.MaxLevel, member.level + 5));
                SaveManager.HealParty();
            }
            SaveManager.SetFlag(UsedFlag);
            bool saved = SaveManager.Instance.TrySave();
            Show("소품지기", saved ? "커튼 뒤에서 작은 기적이 일어났습니다. 바뀐 장면을 기록해 두었습니다." :
                "장면은 바뀌었지만 기록에 실패했습니다. 닫기 전에 저장을 다시 확인해 주세요.",
                new[] { "다른 장면을 준비한다.", "장막을 닫는다." }, i => { if (i == 0) ShowMain(); });
        }
    }
}
