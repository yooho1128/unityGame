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
        private Button _trackButton, _mapButton;
        private Text _trackLabel, _mapLabel;
        private bool _showCompleted;
        private bool _lockedPlayer;
        private bool _refreshPending;
        private QuestDefinition _selectedQuest;
        private QuestProgressData _selectedProgress;
        public bool IsOpen{get;private set;}

        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;if(root!=null&&root.GetComponent<MobileSafeArea>()==null)root.AddComponent<MobileSafeArea>();root?.SetActive(false);entryTemplate?.gameObject.SetActive(false);EnsureTrackingActions();}
        private void OnEnable(){QuestManager.GlobalQuestChanged+=RefreshIfOpen;L10n.Changed+=RefreshIfOpen;SaveManager.CurrentChanged+=RefreshIfOpen;}
        private void OnDisable(){QuestManager.GlobalQuestChanged-=RefreshIfOpen;L10n.Changed-=RefreshIfOpen;SaveManager.CurrentChanged-=RefreshIfOpen;if(IsOpen)Close();}
        private void LateUpdate(){if(IsOpen&&_refreshPending)Refresh();}
        private void OnDestroy(){if(Instance==this)Instance=null;ReleasePlayer();}
        public void Open(){if(IsOpen||SaveManager.Current==null||!CanOpen())return;IsOpen=true;FieldUiModalState.Show(root);if(PlayerController.Instance!=null){PlayerController.Instance.MoveInput=Vector2.zero;PlayerController.Instance.Lock();_lockedPlayer=true;}_showCompleted=false;ResetSelection();Refresh();}
        public void Close(){if(!IsOpen)return;IsOpen=false;root?.SetActive(false);ReleasePlayer();}
        public void ShowActive(){_showCompleted=false;ResetSelection();Refresh();}
        public void ShowCompleted(){_showCompleted=true;ResetSelection();Refresh();}
        private void ResetSelection(){_selectedQuest=null;_selectedProgress=null;if(contentRoot is RectTransform content)content.anchoredPosition=Vector2.zero;}

        private void Refresh()
        {
            _refreshPending=false;
            Vector2 position=UiListLayout.Position(contentRoot);
            string selectedId=_selectedQuest?.questId;
            UiListLayout.Clear(_spawned);var save=SaveManager.Current;if(save==null){Close();return;}
            _selectedQuest=null;_selectedProgress=null;
            int active=0,completed=0,shown=0;
            foreach(var progress in save.quests)
            {
                if(progress.completed)completed++;else active++;
                if(progress.completed!=_showCompleted||!QuestRepository.TryGet(progress.questId,out var quest))continue;
                var view=Instantiate(entryTemplate,contentRoot);view.Bind(quest,progress,Select);_spawned.Add(view.gameObject);shown++;
                if(_selectedQuest==null||quest.questId==selectedId){_selectedQuest=quest;_selectedProgress=progress;}
            }
            if(countText!=null)countText.text=L10n.Format("questlog.count","진행 {0} · 완료 {1}",active,completed);
            if(emptyText!=null)emptyText.gameObject.SetActive(shown==0);
            if(activeFilterButton!=null)activeFilterButton.interactable=_showCompleted;
            if(completedFilterButton!=null)completedFilterButton.interactable=!_showCompleted;
            UiListLayout.Rebuild(contentRoot,position);
            RefreshSelection();
            RefreshDetail();
        }

        private void Select(QuestDefinition quest,QuestProgressData progress){_selectedQuest=quest;_selectedProgress=progress;RefreshSelection();RefreshDetail();}
        private void RefreshSelection(){foreach(var row in _spawned)row.GetComponent<QuestLogEntryView>()?.SetSelected(_selectedQuest?.questId);}
        private void RefreshDetail()
        {
            bool has=_selectedQuest!=null&&_selectedProgress!=null;
            var guidance = has && !_selectedProgress.completed ? QuestGuidanceService.For(_selectedQuest) : null;
            bool pinned = has && SaveManager.Current?.trackedQuestId == _selectedQuest.questId;
            if (_trackButton != null) _trackButton.interactable = has && !_selectedProgress.completed;
            if (_trackLabel != null) _trackLabel.text = pinned ? "기본 추적으로" : "추적 고정";
            if (_mapButton != null) _mapButton.interactable = guidance?.destination != null && WorldMapController.Instance != null;
            if (_mapLabel != null) _mapLabel.text = guidance?.destination == null ? "장소 단서 필요" : QuestManager.Instance?.TrackedQuest?.questId == _selectedQuest.questId ? "목표 지도" : "추적·지도";
            if(titleText!=null)titleText.text=has?L10n.Get($"quest.{_selectedQuest.questId}.title",L10n.Text(_selectedQuest.title)):L10n.Get("questlog.select","기록을 선택하세요");
            if(summaryText!=null)summaryText.text=has?L10n.Get($"quest.{_selectedQuest.questId}.summary",L10n.Text(_selectedQuest.summary)):string.Empty;
            if(objectivesText!=null)
            {
                var b=new StringBuilder();if(has)foreach(var objective in _selectedQuest.objectives){var p=_selectedProgress.objectives.Find(x=>x.objectiveId==objective.objectiveId);int current=p?.current??0,required=Mathf.Max(1,objective.requiredCount);if(b.Length>0)b.AppendLine();b.Append(current>=required?"✓ ":"□ ");b.Append(L10n.Get($"quest.{_selectedQuest.questId}.{objective.objectiveId}",L10n.Text(objective.description)));if(required>1)b.Append($"  {current}/{required}");}if(guidance!=null)b.Append("\n\n다음 행동\n"+guidance.text);objectivesText.text=b.ToString();
            }
            if(rewardText!=null)
            {
                if(!has){rewardText.text=string.Empty;return;}string reward=$"{_selectedQuest.rewardGold:N0} 금화";
                if(!string.IsNullOrEmpty(_selectedQuest.rewardItemId)&&_selectedQuest.rewardItemCount>0){var item=ShadowTheater.Data.ShadowDatabase.Instance?.GetItem(_selectedQuest.rewardItemId);reward+=$" · {(item!=null?L10n.Text(item.displayName):_selectedQuest.rewardItemId)} x{_selectedQuest.rewardItemCount}";}
                rewardText.text=(_selectedProgress.completed?L10n.Get("questlog.received","획득 완료"):L10n.Get("questlog.reward","완료 보상"))+" · "+reward;
            }
        }
        public void ToggleSelectedTracking()
        {
            if (_selectedQuest == null || _selectedProgress == null || _selectedProgress.completed || QuestManager.Instance == null) return;
            string id = SaveManager.Current?.trackedQuestId == _selectedQuest.questId ? null : _selectedQuest.questId;
            bool ok = QuestManager.Instance.TryTrackQuest(id, out string message);
            SaveFeedbackController.Instance?.ShowMessage(message);
            if (ok) Refresh();
        }
        public void OpenSelectedMap()
        {
            if (_selectedQuest == null || _selectedProgress?.completed != false || WorldMapController.Instance == null) return;
            if (QuestGuidanceService.For(_selectedQuest).destination == null) return;
            if (QuestManager.Instance?.TrackedQuest?.questId != _selectedQuest.questId &&
                (QuestManager.Instance == null || !QuestManager.Instance.TryTrackQuest(_selectedQuest.questId, out _)))
            { SaveFeedbackController.Instance?.ShowMessage("목표 추적을 저장하지 못했습니다. 다시 시도해 주세요."); return; }
            Close(); // Release this modal's input lock before opening the map.
            WorldMapController.Instance.OpenForTrackedQuest();
        }
        private void EnsureTrackingActions()
        {
            var detail = titleText != null ? titleText.transform.parent as RectTransform : null;
            if (detail == null) return;
            Place(summaryText?.rectTransform, new Vector2(.06f,.69f), new Vector2(.94f,.84f));
            Place(objectivesText?.rectTransform, new Vector2(.06f,.27f), new Vector2(.94f,.69f));
            Place(rewardText?.rectTransform, new Vector2(.06f,.14f), new Vector2(.94f,.26f));
            if (objectivesText != null)
            { objectivesText.resizeTextForBestFit=true; objectivesText.resizeTextMinSize=16; objectivesText.resizeTextMaxSize=21; }
            _trackButton = ActionButton(detail, "TrackQuest", new Vector2(.06f,.025f), new Vector2(.48f,.12f), ToggleSelectedTracking, out _trackLabel);
            _mapButton = ActionButton(detail, "QuestMap", new Vector2(.52f,.025f), new Vector2(.94f,.12f), OpenSelectedMap, out _mapLabel);
        }
        private Button ActionButton(RectTransform parent, string name, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, out Text label)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,min,max);
            var image=go.GetComponent<Image>(); image.color=new Color(.19f,.26f,.33f);
            var button=go.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var textObject=new GameObject("Label",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(go.transform,false);
            Place((RectTransform)textObject.transform,Vector2.zero,Vector2.one);
            label=textObject.GetComponent<Text>();label.font=titleText.font;label.fontSize=20;label.resizeTextForBestFit=true;
            label.resizeTextMinSize=14;label.resizeTextMaxSize=20;label.color=new Color(.95f,.85f,.63f);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            return button;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        { if(rect==null)return;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private bool CanOpen()=>FieldUiModalState.CanOpen(FieldUiModal.QuestLog);
        private void RefreshIfOpen(){if(IsOpen)_refreshPending=true;}private void ReleasePlayer(){if(!_lockedPlayer)return;PlayerController.Instance?.Unlock();_lockedPlayer=false;}
    }
}
