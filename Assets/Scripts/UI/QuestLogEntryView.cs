using System;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class QuestLogEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text stateText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text progressText;
        private QuestDefinition _quest;
        private QuestProgressData _progress;
        private Action<QuestDefinition, QuestProgressData> _onSelect;
        private bool _bound;
        private void Awake()=>EnsureBinding();
        private void EnsureBinding(){if(_bound)return;if(button==null)button=GetComponent<Button>();if(button!=null){button.onClick.AddListener(Select);_bound=true;}}
        private void OnDestroy()=>button?.onClick.RemoveListener(Select);
        public void Bind(QuestDefinition quest,QuestProgressData progress,Action<QuestDefinition,QuestProgressData> onSelect)
        {
            EnsureBinding();
            _quest=quest;_progress=progress;_onSelect=onSelect;
            if(stateText!=null){stateText.text=progress.completed?"✓":"▶";stateText.color=progress.completed?new Color(.52f,1f,.72f):new Color(.76f,.58f,1f);}
            if(titleText!=null)titleText.text=L10n.Get($"quest.{quest.questId}.title",L10n.Text(quest.title));
            int done=0,total=quest.objectives?.Count??0;
            if(quest.objectives!=null)foreach(var objective in quest.objectives){var p=progress.objectives.Find(x=>x.objectiveId==objective.objectiveId);if(p!=null&&p.current>=Mathf.Max(1,objective.requiredCount))done++;}
            if(progressText!=null)progressText.text=progress.completed?L10n.Get("questlog.complete","완료"):$"{done}/{total}";
            gameObject.SetActive(true);
        }
        public void SetSelected(string questId)
        {
            if(stateText==null||_quest==null||_progress==null)return;
            stateText.text=_quest.questId==questId?"▶":_progress.completed?"✓":"□";
        }
        private void Select()=>_onSelect?.Invoke(_quest,_progress);
    }
}
