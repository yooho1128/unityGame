using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.Field
{
    public sealed class RegionalPuzzlePoint:MonoBehaviour,IInteractable
    {
        [SerializeField]private RegionalPuzzleController owner;
        [SerializeField]private int action;
        private Text label;private SpriteRenderer visual;
        public int Action=>action;
        public void Configure(RegionalPuzzleController controller,int index){owner=controller;action=index;}
        public void Interact(PlayerController player)=>owner?.Interact(action,player);
        private void Start()
        {
            visual=GetComponent<SpriteRenderer>();
            var root=new GameObject("PuzzleLabel",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
            root.transform.localPosition=Vector3.up*.7f;root.transform.localScale=Vector3.one*.011f;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=335;
            ((RectTransform)root.transform).sizeDelta=new Vector2(250,66);
            var plate=RuntimeUiControls.Rect(root.transform,"Plate",Vector2.zero,Vector2.one);
            var image=plate.gameObject.AddComponent<Image>();image.color=new Color(.025f,.045f,.07f,.94f);image.raycastTarget=false;
            label=RuntimeUiControls.Label(plate,LocalizationRuntime.CurrentFont,"",20);owner?.Refresh();
        }
        public void Present(string text,bool active)
        {
            if(label!=null)label.text=text;
            if(visual!=null)visual.color=active?new Color(.65f,1f,.82f):new Color(.65f,.73f,.85f);
        }
    }
}
