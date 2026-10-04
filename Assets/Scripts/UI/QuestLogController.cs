using System.Collections.Generic;
using System.Text;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class QuestLogController : MonoBehaviour
    {
        public static QuestLogController Instance{get;private set;}
        [SerializeField]private GameObject root;
        [SerializeField]private Transform contentRoot;
        [SerializeField]private QuestLogEntryView entryTemplate;
        [SerializeField]private Text countText;
        [SerializeField]private Text titleText;
        [SerializeField]private Text summaryText;
        [SerializeField]private Text objectivesText;
        [SerializeField]private Text rewardText;
        [SerializeField]private Text emptyText;
        [SerializeField]private Button activeFilterButton;
        [SerializeField]private Button completedFilterButton;
        private readonly List<GameObject> _spawned=new List<GameObject>();
        private bool _showCompleted;
        private bool _lockedPlayer;
        private QuestDefinition _selectedQuest;
        private QuestProgressData _selectedProgress;
        public bool IsOpen{get;private set;}

        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;root?.SetActive(false);entryTemplate?.gameObject.SetActive(false);}
        private void OnEnable(){QuestManager.GlobalQuestChanged+=RefreshIfOpen;L10n.Changed+=RefreshIfOpen;}
        private void OnDisable(){QuestManager.GlobalQuestChanged-=RefreshIfOpen;L10n.Changed-=RefreshIfOpen;if(IsOpen)Close();}
        private void OnDestroy(){if(Instance==this)Instance=null;ReleasePlayer();}
        public void Open(){if(IsOpen||SaveManager.Current==null||!CanOpen())return;IsOpen=true;root?.SetActive(true);if(PlayerController.Instance!=null){PlayerController.Instance.MoveInput=Vector2.zero;PlayerController.Instance.Lock();_lockedPlayer=true;}_showCompleted=false;_selectedQuest=null;Refresh();}
        public void Close(){if(!IsOpen)return;IsOpen=false;root?.SetActive(false);ReleasePlayer();}
        public void ShowActive(){_showCompleted=false;_selectedQuest=null;Refresh();}
        public void ShowCompleted(){_showCompleted=true;_selectedQuest=null;Refresh();}

        private void Refresh()
        {
            foreach(var go in _spawned)Destroy(go);_spawned.Clear();var save=SaveManager.Current;if(save==null)return;
            if(_selectedProgress!=null&&_selectedProgress.completed!=_showCompleted){_selectedQuest=null;_selectedProgress=null;}
            int active=0,completed=0,shown=0;
            foreach(var progress in save.quests)
            {
                if(progress.completed)completed++;else active++;
                if(progress.completed!=_showCompleted||!QuestRepository.TryGet(progress.questId,out var quest))continue;
                var view=Instantiate(entryTemplate,contentRoot);view.Bind(quest,progress,Select);_spawned.Add(view.gameObject);shown++;
                if(_selectedQuest==null){_selectedQuest=quest;_selectedProgress=progress;}
            }
            if(countText!=null)countText.text=L10n.Format("questlog.count","진행 {0} · 완료 {1}",active,completed);
            if(emptyText!=null)emptyText.gameObject.SetActive(shown==0);
            if(activeFilterButton!=null)activeFilterButton.interactable=_showCompleted;
            if(completedFilterButton!=null)completedFilterButton.interactable=!_showCompleted;
            if(contentRoot is RectTransform content){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);content.anchoredPosition=new Vector2(content.anchoredPosition.x,0);}
            RefreshDetail();
        }

        private void Select(QuestDefinition quest,QuestProgressData progress){_selectedQuest=quest;_selectedProgress=progress;RefreshDetail();}
        private void RefreshDetail()
        {
            bool has=_selectedQuest!=null&&_selectedProgress!=null;
            if(titleText!=null)titleText.text=has?L10n.Get($"quest.{_selectedQuest.questId}.title",L10n.Text(_selectedQuest.title)):L10n.Get("questlog.select","기록을 선택하세요");
            if(summaryText!=null)summaryText.text=has?L10n.Text(_selectedQuest.summary):string.Empty;
            if(objectivesText!=null)
            {
                var b=new StringBuilder();if(has)foreach(var objective in _selectedQuest.objectives){var p=_selectedProgress.objectives.Find(x=>x.objectiveId==objective.objectiveId);int current=p?.current??0,required=Mathf.Max(1,objective.requiredCount);if(b.Length>0)b.AppendLine();b.Append(current>=required?"✓ ":"□ ");b.Append(L10n.Get($"quest.{_selectedQuest.questId}.{objective.objectiveId}",L10n.Text(objective.description)));if(required>1)b.Append($"  {current}/{required}");}objectivesText.text=b.ToString();
            }
            if(rewardText!=null)
            {
                if(!has){rewardText.text=string.Empty;return;}string reward=$"{_selectedQuest.rewardGold:N0} 금화";
                if(!string.IsNullOrEmpty(_selectedQuest.rewardItemId)&&_selectedQuest.rewardItemCount>0){var item=ShadowTheater.Data.ShadowDatabase.Instance?.GetItem(_selectedQuest.rewardItemId);reward+=$" · {(item!=null?L10n.Text(item.displayName):_selectedQuest.rewardItemId)} x{_selectedQuest.rewardItemCount}";}
                rewardText.text=(_selectedProgress.completed?L10n.Get("questlog.received","획득 완료"):L10n.Get("questlog.reward","완료 보상"))+" · "+reward;
            }
        }
        private bool CanOpen(){if(GameFlowController.Instance!=null&&GameFlowController.Instance.IsInBattle)return false;if(DialogueController.Instance!=null&&DialogueController.Instance.IsPlaying)return false;if(ScriptBookController.Instance!=null&&ScriptBookController.Instance.IsOpen)return false;if(PartyStorageController.Instance!=null&&PartyStorageController.Instance.IsOpen)return false;if(WorldMapController.Instance!=null&&WorldMapController.Instance.IsOpen)return false;if(SettlementShopController.Instance!=null&&SettlementShopController.Instance.IsOpen)return false;if(FieldInventoryController.Instance!=null&&FieldInventoryController.Instance.IsOpen)return false;return true;}
        private void RefreshIfOpen(){if(IsOpen)Refresh();}private void ReleasePlayer(){if(!_lockedPlayer)return;PlayerController.Instance?.Unlock();_lockedPlayer=false;}
    }
}
