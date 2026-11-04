using ShadowTheater.Field;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Readable keyboard keycaps and a separate controller row, including older scenes.</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopControlsView : MonoBehaviour
    {
        private RectTransform _rect;
        private CanvasGroup _group;
        private Font _font;
        private Text[] _labels;
        private Text _keyboard, _gamepad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (Application.isMobilePlatform) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var display in root.GetComponentsInChildren<PlatformDisplayGroup>(true))
                    if (display.name == "DesktopControls" && display.GetComponent<DesktopControlsView>() == null)
                        display.gameObject.AddComponent<DesktopControlsView>();
        }

        private void OnEnable()
        {
            L10n.Changed += RefreshLabels;
            RefreshLabels();
        }
        private void OnDisable() => L10n.Changed -= RefreshLabels;

        private void Start()
        {
            var oldHint = transform.Find("Hint")?.GetComponent<Text>();
            if (oldHint == null || oldHint.font == null) return;
            _font = oldHint.font;
            oldHint.gameObject.SetActive(false);
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2(28f, 28f);
            _rect.sizeDelta = new Vector2(720f, 156f);
            _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _group.interactable = _group.blocksRaycasts = false;
            _group.alpha = ShouldHide() ? 0f : 1f;
            var plate = GetComponent<Image>();
            TheatreUiStyle.Panel(plate, new Color(.035f, .055f, .09f, .96f));
            if (plate != null) plate.raycastTarget = false;
            Box("TopRule", 18, 153, 684, 2, TheatreUiStyle.Brass);
            Box("Divider", 18, 46, 684, 1, TheatreUiStyle.Raised);
            _keyboard = Label("Keyboard", 18, 125, 660, 22, 15, TheatreUiStyle.Brass);
            _labels = new Text[12];
            _labels[0] = Key("WASD / ↑↓←→", 18, 89, 142, 94);
            _labels[1] = Key("Z / Space / Enter", 267, 89, 174, 90);
            _labels[2] = Key("Esc", 548, 89, 48, 102);
            string[] keys = { "I", "P", "Tab", "M", "Q", "O" };
            for (int i = 0; i < keys.Length; i++)
                _labels[i + 3] = Key(keys[i], 18 + i * 115, 52, 36, 70);
            _gamepad = Label("Gamepad", 18, 9, 104, 28, 15, TheatreUiStyle.Mint);
            _labels[9] = Key("Stick / D-Pad", 132, 9, 142, 82, true);
            _labels[10] = Key("A", 380, 9, 36, 112, true);
            _labels[11] = Key("B", 548, 9, 36, 112, true);
            RefreshLabels();
        }

        private static bool ShouldHide() => FieldUiModalState.AnyOpen ||
            (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) ||
            (MapLoader.Instance != null && MapLoader.Instance.IsTransitioning);

        private void Update()
        {
            if (_group == null) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, ShouldHide() ? 0f : 1f, Time.unscaledDeltaTime * 8f);
            var parent = _rect.parent as RectTransform;
            if (parent != null)
                _rect.localScale = Vector3.one * Mathf.Min(1f, Mathf.Max(1f, parent.rect.width - 56f) / 720f);
        }

        private void RefreshLabels()
        {
            if (_labels == null) return;
            _keyboard.text = L10n.Get("controls.keyboard", "키보드");
            _gamepad.text = L10n.Get("controls.gamepad", "게임패드");
            string[] keys = { "controls.move", "field.action_inspect", "field.menu", "controls.inventory",
                "field.party", "field.script_book", "controls.map", "controls.quests", "field.shop",
                "controls.move", "controls.select", "controls.back" };
            string[] fallback = { "이동", "조사", "메뉴", "가방", "파티", "각본집", "지도", "퀘스트", "상점", "이동", "선택", "뒤로" };
            for (int i = 0; i < _labels.Length; i++) _labels[i].text = L10n.Get(keys[i], fallback[i]);
        }

        private Text Key(string key, float x, float y, float width, float labelWidth, bool pad = false)
        {
            var cap = Box("Key_" + key, x, y, width, 28, TheatreUiStyle.Raised);
            var text = RuntimeUiControls.Label(cap.rectTransform, _font, key, 17);
            text.color = pad ? TheatreUiStyle.Mint : TheatreUiStyle.Ivory;
            text.fontStyle = FontStyle.Bold;
            return Label("Action", x + width + 8, y, labelWidth, 28, 20, TheatreUiStyle.Ivory);
        }

        private Image Box(string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, float x, float y, float width, float height, int size, Color color)
        {
            var label = Rect(name, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = _font; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size;
            return label;
        }

        private RectTransform Rect(string name, float x, float y, float width, float height)
        {
            var rect = RuntimeUiControls.Rect(transform, name, Vector2.zero, Vector2.zero);
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
