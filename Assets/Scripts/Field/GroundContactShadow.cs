using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>캐릭터 발밑의 접지 그림자를 공유 스프라이트 하나로 표현한다.</summary>
    public class GroundContactShadow : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private SpriteRenderer shadow;
        public void Configure(SpriteRenderer renderer) { target = renderer; EnsureShadow(); }
        private void OnEnable() => EnsureShadow();
        private void EnsureShadow()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (target == null) return;
            if (shadow == null)
            {
                var child = new GameObject("ContactShadow", typeof(SpriteRenderer));
                child.transform.SetParent(transform, false); shadow = child.GetComponent<SpriteRenderer>();
            }
            shadow.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.ContactShadow);
            shadow.color = new Color(.015f, .01f, .035f, .3f);
            shadow.sortingLayerID = target.sortingLayerID; shadow.sortingOrder = target.sortingOrder - 1;
            shadow.transform.localPosition = new Vector3(0, -.025f, 0);
            shadow.transform.localScale = new Vector3(.64f, .64f, 1);
        }
        private void LateUpdate()
        {
            if (target == null || shadow == null) return;
            shadow.enabled = target.enabled;
            shadow.sortingLayerID = target.sortingLayerID;
            shadow.sortingOrder = target.sortingOrder - 1;
            float height = Mathf.Max(0, target.bounds.min.y - transform.position.y);
            float size = Mathf.Clamp(.64f - height * .18f, .4f, .68f);
            shadow.transform.localScale = new Vector3(size, size, 1);
            shadow.color = new Color(.015f, .01f, .035f, Mathf.Clamp(.3f - height * .15f, .12f, .3f) * target.color.a);
        }
    }
}
