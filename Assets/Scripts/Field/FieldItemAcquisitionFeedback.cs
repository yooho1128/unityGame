using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>가방 수량이 늘면 플레이어 머리 위에 획득 도구를 잠깐 표시한다.</summary>
    public sealed class FieldItemAcquisitionFeedback:MonoBehaviour
    {
        private readonly Dictionary<string,int> _counts=new Dictionary<string,int>();
        private readonly Queue<string> _messages=new Queue<string>();
        private TextMesh _label;
        private Coroutine _routine;
        private IEnumerator Start()
        {
            while(SaveManager.Current==null)yield return null;
            Snapshot();SaveManager.InventoryChanged+=OnInventoryChanged;
            var go=new GameObject("ItemAcquisitionLabel",typeof(TextMesh));go.transform.SetParent(transform,false);
            go.transform.localPosition=new Vector3(0f,1.25f,0f);_label=go.GetComponent<TextMesh>();
            _label.anchor=TextAnchor.MiddleCenter;_label.alignment=TextAlignment.Center;_label.fontSize=42;
            _label.characterSize=.055f;_label.color=new Color(1f,.88f,.32f,0f);
            go.GetComponent<MeshRenderer>().sortingOrder=320;
        }
        private void OnDestroy()=>SaveManager.InventoryChanged-=OnInventoryChanged;
        private void Snapshot()
        {
            _counts.Clear();if(SaveManager.Current?.inventory==null)return;
            foreach(ItemStack stack in SaveManager.Current.inventory)if(stack!=null)_counts[stack.itemId]=stack.count;
        }
        private void OnInventoryChanged()
        {
            if(SaveManager.Current?.inventory==null)return;
            foreach(ItemStack stack in SaveManager.Current.inventory)
            {
                if(stack==null)continue;_counts.TryGetValue(stack.itemId,out int before);
                int gained=stack.count-before;if(gained<=0)continue;
                ItemData item=ShadowDatabase.Instance?.GetItem(stack.itemId);
                string name=item!=null?L10n.Text(item.displayName):stack.itemId;
                _messages.Enqueue($"{name}  +{gained}");
            }
            Snapshot();if(_routine==null&&_label!=null)_routine=StartCoroutine(ShowQueue());
        }
        private IEnumerator ShowQueue()
        {
            while(_messages.Count>0)
            {
                _label.text=_messages.Dequeue();Vector3 start=new Vector3(0f,1.12f,0f);
                for(float elapsed=0f;elapsed<1.15f;elapsed+=Time.unscaledDeltaTime)
                {
                    float t=Mathf.Clamp01(elapsed/1.15f);_label.transform.localPosition=start+Vector3.up*(t*.32f);
                    Color c=_label.color;c.a=Mathf.Sin(t*Mathf.PI);_label.color=c;yield return null;
                }
            }
            _label.text=string.Empty;_routine=null;
        }
    }
}
