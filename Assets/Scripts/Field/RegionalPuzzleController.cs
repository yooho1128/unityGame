using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShadowTheater.Field
{
    /// <summary>Optional three-mechanic field puzzle. Only a local shortcut is unlocked.</summary>
    public sealed class RegionalPuzzleController:MonoBehaviour
    {
        [SerializeField]private string regionId;
        private RegionalPuzzleState state;
        private string saveGuid;
        private int lastFrame=-1;
        private PlayerController lockedPlayer;
        private RegionalPuzzleDefinition Definition=>RegionalPuzzleCatalog.Get(regionId);
        private void OnEnable()=>SaveManager.CurrentChanged+=ResetState;
        private void OnDisable(){SaveManager.CurrentChanged-=ResetState;Release();}
        private void Start()=>ResetState();
        private void ResetState()
        {
            var d=Definition;if(d==null)return;
            saveGuid=SaveManager.Current?.saveGuid;state=new RegionalPuzzleState(d,SaveManager.HasFlag(d.CompletionFlag));Refresh();
        }
        public static void Ensure(Transform root,RegionData region)
        {
            var d=RegionalPuzzleCatalog.Get(region?.regionId);var grid=root.GetComponentInChildren<FieldGrid>(true);
            if(d==null||grid==null||root.GetComponentInChildren<RegionalPuzzleController>(true)!=null)return;
            if(!RegionalPuzzleState.Valid(d)){Debug.LogError("[RegionalPuzzle] Invalid definition: "+d.id);return;}
            var go=new GameObject("RegionalPuzzle");go.transform.SetParent(root,false);
            var controller=go.AddComponent<RegionalPuzzleController>();controller.regionId=region.regionId;
            var used=new List<LayoutCell>{new LayoutCell(region.arrivalX,region.arrivalY)};
            foreach(var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if(collider is TilemapCollider2D||collider is CompositeCollider2D)continue;
                var cell=grid.WorldToCell(collider.transform.position);used.Add(new LayoutCell(cell.x,cell.y));
            }
            foreach(var portal in root.GetComponentsInChildren<MapPortal>(true))
            {
                var c=grid.WorldToCell(portal.transform.position);
                used.Add(new LayoutCell(c.x-1,c.y));used.Add(new LayoutCell(c.x+1,c.y));
                used.Add(new LayoutCell(c.x,c.y-1));used.Add(new LayoutCell(c.x,c.y+1));
            }
            var hub=JourneyHubPolicy.Get(region);if(hub!=null)used.Add(new LayoutCell(hub.checkpointX,hub.checkpointY));
            var planned=new List<LayoutCell>{d.board};planned.AddRange(d.nodes);planned.AddRange(d.exits);
            var places=new List<LayoutCell>();
            foreach(var preferred in planned)
            {
                var cell=JourneyHubPolicy.StationCell(new JourneyHubPolicy.Definition{x=preferred.x,y=preferred.y,checkpointX=99,checkpointY=99},used);
                if(!cell.HasValue){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);return;}
                places.Add(cell.Value);used.Add(cell.Value);
            }
            for(int i=0;i<places.Count;i++)
            {
                int action=i==0?-1:i<=d.nodes.Length?i-1:i==places.Count-2?-2:-3;
                var point=new GameObject("PuzzlePoint_"+i,typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(RegionalPuzzlePoint));
                point.transform.SetParent(go.transform,false);point.transform.position=grid.CellToWorld(new Vector2Int(places[i].x,places[i].y));
                var collider=point.GetComponent<BoxCollider2D>();collider.size=new Vector2(.65f,.65f);collider.isTrigger=action<=-2;
                point.GetComponent<SpriteRenderer>().sprite=Resources.LoadAll<Sprite>("Art/Environment/ReleaseTerrain").FirstOrDefault(s=>s.name==(d.kind=="Sequence"?"Brick":"Wood"));
                point.GetComponent<SpriteRenderer>().sortingOrder=13;
                point.GetComponent<RegionalPuzzlePoint>().Configure(controller,action);
            }
        }
        public void Interact(int action,PlayerController player)
        {
            if(action<-3||player==null||player.IsLocked||player.IsMoving||SaveManager.Current==null||Time.frameCount==lastFrame)return;
            if(!FieldUiModalState.CanOpen(FieldUiModal.Dialogue))return;
            lastFrame=Time.frameCount;if(state==null||saveGuid!=SaveManager.Current.saveGuid)ResetState();
            var d=Definition;if(state==null||d==null)return;
            if(action==-1)
            {
                var dialogue=new DialogueSequence{dialogueId="regional_puzzle_hint_"+d.id};
                dialogue.lines.Add(new DialogueLine{speaker=d.title,text=state.Solved?d.completionText:d.clue});
                dialogue.lines.Add(new DialogueLine{speaker=d.title,text=state.Summary+". 완료하면 남서쪽과 남동쪽의 지름길을 이용할 수 있습니다."});
                DialogueController.Instance?.PlayDynamic(dialogue,new Color(.64f,.87f,.78f));return;
            }
            if(action<=-2)
            {
                if(!state.Solved){SaveFeedbackController.Instance?.ShowMessage("단서판을 읽고 장치를 복원하면 지름길이 열립니다.");return;}
                var target=GetComponentsInChildren<RegionalPuzzlePoint>(true).FirstOrDefault(p=>p.Action==(action==-2?-3:-2));
                if(target!=null)StartCoroutine(Travel(player,target.transform.position));return;
            }
            if(!state.Activate(action)){SaveFeedbackController.Instance?.ShowMessage(state.Summary);return;}
            if(state.Solved)
            {
                SaveManager.SetFlag(d.CompletionFlag);
                bool saved=SaveManager.Instance!=null&&SaveManager.Instance.TrySave();
                SaveFeedbackController.Instance?.ShowMessage(d.completionText+(saved?"":"\n기억 저장에 실패했습니다. 다시 저장해 주세요."));
            }
            else SaveFeedbackController.Instance?.ShowMessage(state.Summary);
            Refresh();
        }
        public void Refresh()
        {
            var d=Definition;if(d==null||state==null)return;
            foreach(var point in GetComponentsInChildren<RegionalPuzzlePoint>(true))
            {
                int action=point.Action;
                string text=action==-1?d.title+"\n단서 읽기":action<0?"지름길\n"+(state.Solved?"조사하여 이동":"복원 필요"):
                    d.nodeNames[action]+"\n"+state.Status(action);
                point.Present(text,state.Solved||(action>=0&&(d.kind=="Toggle"?((state.Mask&(1<<action))!=0):state.Status(action).StartsWith("✓",StringComparison.Ordinal))));
            }
        }
        private IEnumerator Travel(PlayerController player,Vector3 target)
        {
            lockedPlayer=player;player.Lock();
            try
            {
                var fader=ScreenFader.Instance;if(fader!=null)yield return fader.FadeOut(.18f);
                if(FieldGrid.Current!=null&&player.SnapToSafeCell(FieldGrid.Current.WorldToCell(target),player.Cell,out _))
                {FieldCameraFollow.Instance?.Snap();SaveManager.Instance?.TrySave();}
                else SaveFeedbackController.Instance?.ShowMessage("지름길의 도착 지점을 확인할 수 없습니다.");
                if(fader!=null)yield return fader.FadeIn(.18f);
            }
            finally{Release();}
        }
        private void Release(){if(lockedPlayer!=null)lockedPlayer.Unlock();lockedPlayer=null;}
    }
}
