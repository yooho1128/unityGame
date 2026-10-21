using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>야외 상태를 보존한 채 같은 필드 중앙에 걸어 다닐 수 있는 작은 마을 실내를 연다.</summary>
    public sealed class TownInteriorController : MonoBehaviour
    {
        public static TownInteriorController Instance{get;private set;}
        [SerializeField]private string regionId;
        [SerializeField]private Sprite floorSprite;
        [SerializeField]private Sprite wallSprite;
        [SerializeField]private Color accent=new Color(.72f,.52f,.34f,1f);
        private readonly List<Renderer> _renderers=new List<Renderer>();
        private readonly List<Collider2D> _colliders=new List<Collider2D>();
        private GameObject _room;private PlayerController _player;private Vector2Int _returnCell;private FacingDir _returnFacing;
        private ShadowCompanionFollower _companionFollower;
        public bool IsInside=>_room!=null;
        public bool TryGetOutdoorPosition(out Vector2Int cell,out FacingDir facing)
        {cell=_returnCell;facing=_returnFacing;return IsInside;}
        public void Configure(string id,Sprite floor,Sprite wall,Color color){regionId=id;floorSprite=floor;wallSprite=wall;accent=color;}
        private void Awake()=>Instance=this;
        private void OnDestroy(){if(Instance==this)Instance=null;RestoreExterior();}

        public void Enter(PlayerController player,string roomName)
        {
            if(IsInside||player==null||FieldGrid.Current==null)return;
            _player=player;_returnCell=player.Cell;_returnFacing=player.Facing;player.Lock();
            _companionFollower=GetComponent<ShadowCompanionFollower>();if(_companionFollower!=null)_companionFollower.enabled=false;
            StartCoroutine(EnterRoutine(roomName));
        }
        private IEnumerator EnterRoutine(string roomName)
        {
            ScreenFader fader=ScreenFader.Instance;if(fader!=null)yield return fader.FadeOut();
            HideExterior();BuildRoom();_player.SnapToCell(new Vector2Int(0,-1));_player.SetFacing(FacingDir.Up);
            FieldCameraFollow.Instance?.Snap();SaveManager.SetFlag("interior_visited_"+regionId);SaveManager.Instance?.TrySave(false);
            if(fader!=null)yield return fader.FadeIn();
            _player.Unlock();
            SaveFeedbackController.Instance?.ShowMessage(roomName+"에 들어왔습니다. 아래쪽 문으로 나갈 수 있습니다.");
        }
        public void Exit()
        {
            if(!IsInside||_player==null)return;_player.Lock();StartCoroutine(ExitRoutine());
        }
        private IEnumerator ExitRoutine()
        {
            ScreenFader fader=ScreenFader.Instance;if(fader!=null)yield return fader.FadeOut();
            _room.SetActive(false);Destroy(_room);_room=null;RestoreExterior();_player.SnapToCell(_returnCell);_player.SetFacing(_returnFacing);
            if(_companionFollower!=null)_companionFollower.enabled=true;
            FieldCameraFollow.Instance?.Snap();if(fader!=null)yield return fader.FadeIn();_player.Unlock();
        }
        private void HideExterior()
        {
            _renderers.Clear();_colliders.Clear();
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if(renderer.enabled&&!renderer.transform.IsChildOf(_player.transform)){_renderers.Add(renderer);renderer.enabled=false;}
            foreach(Collider2D collider in GetComponentsInChildren<Collider2D>(true))
                if(collider.enabled&&!collider.transform.IsChildOf(_player.transform)){_colliders.Add(collider);collider.enabled=false;}
        }
        private void RestoreExterior()
        {
            foreach(Renderer renderer in _renderers)if(renderer!=null)renderer.enabled=true;
            foreach(Collider2D collider in _colliders)if(collider!=null)collider.enabled=true;
            _renderers.Clear();_colliders.Clear();
        }
        private void BuildRoom()
        {
            _room=new GameObject("TownInteriorRoom");_room.transform.SetParent(transform,false);
            CreatePart("Floor",Vector2Int.zero,new Vector2(7.4f,5.2f),floorSprite,Color.Lerp(accent,new Color(.44f,.31f,.22f),.55f),120,false);
            CreatePart("BackWall",new Vector2Int(0,2),new Vector2(7.5f,.65f),wallSprite,Color.Lerp(accent,Color.black,.25f),230,true);
            CreatePart("LeftWall",new Vector2Int(-4,0),new Vector2(.65f,5.4f),wallSprite,accent,230,true);
            CreatePart("RightWall",new Vector2Int(4,0),new Vector2(.65f,5.4f),wallSprite,accent,230,true);
            CreatePart("BottomLeft",new Vector2Int(-2,-2),new Vector2(3.2f,.65f),wallSprite,accent,230,true);
            CreatePart("BottomRight",new Vector2Int(2,-2),new Vector2(3.2f,.65f),wallSprite,accent,230,true);
            CreatePart("Table",new Vector2Int(1,1),new Vector2(1.4f,.72f),wallSprite,new Color(.55f,.34f,.2f),FieldDepthSorter.OrderAt(1f,-2),true);
            CreatePart("Bed",new Vector2Int(-2,1),new Vector2(1.6f,.8f),floorSprite,Color.Lerp(accent,Color.white,.2f),FieldDepthSorter.OrderAt(1f,-2),true);
            var exit=new GameObject("InteriorExit",typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(TownInteriorExit));
            exit.transform.SetParent(_room.transform);exit.transform.position=FieldGrid.Current.CellToWorld(new Vector2Int(0,-2));
            var r=exit.GetComponent<SpriteRenderer>();r.sprite=wallSprite;r.color=new Color(.48f,.28f,.18f);r.sortingOrder=225;
            exit.transform.localScale=new Vector3(.8f,.7f,1f);exit.GetComponent<TownInteriorExit>().Configure(this);
        }
        private void CreatePart(string name,Vector2Int cell,Vector2 scale,Sprite sprite,Color color,int order,bool collision)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(_room.transform);go.transform.position=FieldGrid.Current.CellToWorld(cell);
            go.transform.localScale=new Vector3(scale.x,scale.y,1f);var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.color=color;r.sortingOrder=order;
            if(collision)go.AddComponent<BoxCollider2D>().size=new Vector2(.9f,.9f);
        }
    }

}
