using UnityEngine;

namespace ShadowTheater.UI
{
    /// <summary>완성 컬러 초상은 원색으로, 기능 테스트용 흰 실루엣은 검은 잉크로 표시한다.</summary>
    public static class ShadowPortraitStyle
    {
        public static Color Tint(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return Color.black;
            return sprite.texture.width >= 512 && sprite.texture.height >= 512 ? Color.white : Color.black;
        }
    }
}
