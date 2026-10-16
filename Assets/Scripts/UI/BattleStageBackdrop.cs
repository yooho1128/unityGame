using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    [RequireComponent(typeof(Image))]
    public class BattleStageBackdrop : MonoBehaviour
    {
        private void OnEnable()
        {
            RegionData region = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            string theme = ThemeForAct(region?.act ?? 1);
            Sprite sprite = Resources.Load<Sprite>("Art/BattleBackgrounds/" + theme);
            if (sprite == null) sprite = Resources.Load<Sprite>("Art/BattleBackgrounds/Theater");
            if (sprite != null) { GetComponent<Image>().sprite = sprite; GetComponent<Image>().color = Color.white; }
        }
        public static string ThemeForAct(int act) => act == 2 ? "Ember" : act == 3 || act == 6 ? "Archive" :
            act == 7 ? "Ocean" : act == 8 ? "Cosmos" : "Theater";
    }
}
