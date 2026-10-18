using ShadowTheater.Field;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>
    /// 필드에서 전체 화면을 점유하는 UI의 단일 진입 규칙.
    /// 각 화면이 서로 다른 목록을 검사하면 버튼을 같은 프레임에 누를 때 UI가 겹칠 수 있으므로
    /// 모든 메뉴와 대화가 이 클래스만 통해 열림 여부를 판단한다.
    /// </summary>
    public enum FieldUiModal
    {
        Dialogue,
        ScriptBook,
        Party,
        WorldMap,
        Shop,
        Inventory,
        QuestLog,
        BossJournal,
        Pause
    }

    public static class FieldUiModalState
    {
        // Field menus live on separate HUD canvases. Lift only the active panel above
        // every dock, while leaving transitions (100) and system feedback visible.
        public static void Show(GameObject root)
        {
            if (root == null) return;
            root.SetActive(true);
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 80;
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
        }

        public static bool CanOpen(FieldUiModal requested)
        {
            if (MapLoader.Instance != null && MapLoader.Instance.IsTransitioning) return false;
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return false;
            if (requested != FieldUiModal.Dialogue && DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return false;
            if (requested != FieldUiModal.ScriptBook && ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.Party && PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.WorldMap && WorldMapController.Instance != null && WorldMapController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.Shop && SettlementShopController.Instance != null && SettlementShopController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.Inventory && FieldInventoryController.Instance != null && FieldInventoryController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.QuestLog && QuestLogController.Instance != null && QuestLogController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.BossJournal && BossInvestigationJournalController.Instance != null && BossInvestigationJournalController.Instance.IsOpen) return false;
            if (requested != FieldUiModal.Pause && FieldPauseMenuController.Instance != null && FieldPauseMenuController.Instance.IsOpen) return false;
            return true;
        }

        public static bool AnyOpen =>
            (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) ||
            (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) ||
            (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) ||
            (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen) ||
            (SettlementShopController.Instance != null && SettlementShopController.Instance.IsOpen) ||
            (FieldInventoryController.Instance != null && FieldInventoryController.Instance.IsOpen) ||
            (QuestLogController.Instance != null && QuestLogController.Instance.IsOpen) ||
            (BossInvestigationJournalController.Instance != null && BossInvestigationJournalController.Instance.IsOpen) ||
            (FieldPauseMenuController.Instance != null && FieldPauseMenuController.Instance.IsOpen);
    }
}
