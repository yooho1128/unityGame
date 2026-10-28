#if UNITY_EDITOR
using System.Collections.Generic;
using ShadowTheater.Story;

namespace ShadowTheater.EditorTools
{
    public static class BackstageEasterEggRegression
    {
        public static void Validate(List<string> errors)
        {
            var ritual = new BackstageRitual();
            ritual.Use(BackstageRitual.ItemId);
            if (ritual.Ready || ritual.Rehearsals != 0) errors.Add("커튼 뒤: 선행 조사 없는 아이템 사용 허용");
            for (int i = 0; i < 3; i++) ritual.Read();
            for (int i = 0; i < 2; i++) ritual.Use(BackstageRitual.ItemId);
            if (ritual.Ready) errors.Add("커튼 뒤: 대본 2회로 열림");
            ritual.Use(BackstageRitual.ItemId);
            if (!ritual.Ready) errors.Add("커튼 뒤: 조사3+대본3 조합 실패");
            ritual.Use(BackstageRitual.ItemId);
            if (ritual.Ready) errors.Add("커튼 뒤: 대본 초과 사용 허용");
            for (int i = 0; i < 3; i++) ritual.Read();
            ritual.Use("potion");
            if (ritual.Ready || ritual.Readings != 0 || ritual.Rehearsals != 0)
                errors.Add("커튼 뒤: 잘못된 도구 초기화 실패");
            for (int i = 0; i < 3; i++) ritual.Read();
            ritual.Read();
            if (ritual.Readings != 1) errors.Add("커튼 뒤: 조사 초과 초기화 실패");
            ritual.Reset();
            if (ritual.Ready || ritual.Readings != 0 || ritual.Rehearsals != 0)
                errors.Add("커튼 뒤: 이동/씬 전환 초기화 실패");
        }
    }
}
#endif
