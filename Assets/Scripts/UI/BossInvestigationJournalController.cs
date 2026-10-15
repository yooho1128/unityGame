using System.Collections.Generic;
using System.Text;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>발견한 보스 단서와 NPC·지역 출처를 한 화면에 보존하는 조사 수첩.</summary>
    public class BossInvestigationJournalController : MonoBehaviour
    {
        public static BossInvestigationJournalController Instance { get; private set; }
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private GameObject entryTemplate;
        [SerializeField] private Text progressText;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _lockedPlayer;
        public bool IsOpen { get; private set; }

        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;root?.SetActive(false);entryTemplate?.SetActive(false);}
        private void OnDestroy(){if(Instance==this)Instance=null;ReleasePlayer();}
        private void OnDisable(){if(IsOpen)Close();}
        public void Open(){if(IsOpen||!FieldUiModalState.CanOpen(FieldUiModal.BossJournal))return;IsOpen=true;root?.SetActive(true);if(PlayerController.Instance!=null){PlayerController.Instance.MoveInput=Vector2.zero;PlayerController.Instance.Lock();_lockedPlayer=true;}Refresh();}
        public void Close(){if(!IsOpen)return;IsOpen=false;root?.SetActive(false);ReleasePlayer();}

        private void Refresh()
        {
            foreach(GameObject value in _spawned)Destroy(value);_spawned.Clear();
            var entries=BossInvestigationService.JournalEntries();int found=0,total=entries.Count*BossInvestigationService.TotalClues;
            foreach(BossInvestigationSnapshot entry in entries)
            {
                found+=entry.cluesFound;var item=Instantiate(entryTemplate,contentRoot);item.SetActive(true);var text=item.GetComponentInChildren<Text>();
                var b=new StringBuilder();b.AppendLine($"{entry.bossName}  [{entry.cluesFound}/{entry.totalClues}] · 예상 출현지 {entry.bossRegionName}");
                for(int i=1;i<=entry.totalClues;i++)b.AppendLine($"{(i<=entry.cluesFound?"✓":"□")} {BossInvestigationService.ClueText(entry,i)}\n   출처: {(i<=entry.cluesFound?BossInvestigationService.ClueSource(entry,i):"미확인")}");
                if(text!=null)text.text=b.ToString().TrimEnd();_spawned.Add(item);
            }
            if(progressText!=null)progressText.text=$"발견한 단서 {found}/{total}";
            if(contentRoot is RectTransform content){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);content.anchoredPosition=new Vector2(content.anchoredPosition.x,0);}
        }
        private void ReleasePlayer(){if(!_lockedPlayer)return;PlayerController.Instance?.Unlock();_lockedPlayer=false;}
    }
}
