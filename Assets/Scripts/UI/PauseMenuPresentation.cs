using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public static class PauseMenuPresentation
    {
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            var backdrop = root.GetComponent<Image>();
            if (backdrop != null) backdrop.color = new Color(.02f, .03f, .05f, .94f);
            var card = root.transform.Find("Card");
            if (card == null) return;
            TheatreUiStyle.Panel(card.GetComponent<Image>(), TheatreUiStyle.Surface);
            foreach (var button in card.GetComponentsInChildren<Button>(true))
                TheatreUiStyle.Button(button, button.name == "Save");
            foreach (var slider in card.GetComponentsInChildren<Slider>(true)) TheatreUiStyle.Slider(slider);
            foreach (var text in card.GetComponentsInChildren<Text>(true))
            {
                text.raycastTarget = false;
                if (text.transform.parent.GetComponent<Button>() != null) continue;
                text.color = text.name == "Gold" ? TheatreUiStyle.Brass : TheatreUiStyle.Ivory;
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = Mathf.Min(26, text.fontSize);
            }
            var settingsLabel = card.Find("FullSettingsButton")?.GetComponentInChildren<Text>();
            if (settingsLabel != null) settingsLabel.text = L10n.Get("settings.heading", "설정");
            if (Screen.width < Screen.height * 1.2f)
            {
                Place(card, "Header", .07f, .91f, .62f, .98f, 34);
                Place(card, "FullSettingsButton", .65f, .92f, .83f, .975f);
                return;
            }
            Place(card, "Header", .045f, .87f, .65f, .97f, 34);
            Place(card, "FullSettingsButton", .70f, .885f, .88f, .96f);
            RuntimeUiControls.Place((RectTransform)card, new Vector2(.12f, .09f), new Vector2(.88f, .91f));
            Backing(card, "JourneyCard", .045f, .38f, .485f, .84f);
            Backing(card, "AudioCard", .505f, .38f, .955f, .84f);
            Place(card, "Close", .905f, .885f, .96f, .96f);
            Place(card, "Region", .065f, .73f, .465f, .80f, 26);
            Place(card, "PlayTime", .065f, .65f, .465f, .71f, 20);
            Place(card, "Gold", .065f, .58f, .465f, .64f, 22);
            var gold = card.Find("Gold")?.GetComponent<Text>();
            if (gold != null) gold.alignment = TextAnchor.MiddleLeft;
            Place(card, "Journey", .065f, .405f, .465f, .555f, 20);
            Place(card, "AudioHeader", .525f, .765f, .77f, .825f, 23);
            Place(card, "전투 복기", .79f, .765f, .935f, .825f);
            string[] names = { "전체 음량", "음악", "환경음", "효과음" };
            for (int i = 0; i < names.Length; i++)
            {
                float bottom = .65f - i * .085f;
                Place(card, names[i] + "Label", .525f, bottom + .045f, .80f, bottom + .085f, 18);
                Place(card, names[i] + "Value", .83f, bottom + .045f, .935f, bottom + .085f, 18);
                Place(card, names[i] + "Slider", .52f, bottom, .94f, bottom + .045f);
            }
            Place(card, "Feedback", .045f, .315f, .955f, .365f, 18);
            string[] shortcuts = { "Party", "Inventory", "QuestLog", "BossJournal" };
            for (int i = 0; i < shortcuts.Length; i++)
                Place(card, shortcuts[i], .045f + i * .23f, .20f, .265f + i * .23f, .29f);
            Place(card, "Save", .045f, .06f, .335f, .15f);
            Place(card, "RecoverPosition", .355f, .06f, .645f, .15f);
            Place(card, "ReturnTitle", .665f, .06f, .955f, .15f);
            var confirm = root.transform.Find("TitleConfirm");
            if (confirm != null) TheatreUiStyle.Panel(confirm.GetComponent<Image>(), TheatreUiStyle.Surface);
        }

        private static void Backing(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) rect = RuntimeUiControls.Rect(parent, name, Vector2.zero, Vector2.one);
            RuntimeUiControls.Place(rect, new Vector2(x0, y0), new Vector2(x1, y1));
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            TheatreUiStyle.SurfaceImage(image, new Color(.095f, .125f, .175f), 12, false);
            image.raycastTarget = false; rect.SetAsFirstSibling();
        }

        private static void Place(Transform parent, string name, float x0, float y0, float x1, float y1, int size = 0)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) return;
            RuntimeUiControls.Place(rect, new Vector2(x0, y0), new Vector2(x1, y1));
            var text = rect.GetComponent<Text>();
            if (text != null && size > 0) { text.fontSize = size; text.resizeTextMaxSize = size; }
        }
    }
}
