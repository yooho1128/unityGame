#if UNITY_EDITOR
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>재생성 뒤에도 Final 폴더의 제작 음원과 이펙트를 우선 연결한다.</summary>
    public static class ReleaseAssetOverrides
    {
        public static T Prefer<T>(string finalPath, T fallback) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(finalPath) ?? fallback;

        public static AudioClip SkillAudio(SkillData skill, bool impact, AudioClip fallback)
        {
            string suffix = impact ? "Impact" : "Cast";
            string category = skill.isUltimate ? "Ultimate" : "Skills";
            string theme = skill.isUltimate ? skill.ultimateFxStyle.ToString() : skill.element.ToString();
            AudioClip shared = Prefer($"Assets/Audio/Final/{category}/{theme}_{suffix}.wav", fallback);
            return Prefer($"Assets/Audio/Final/Skills/{skill.skillId}_{suffix}.wav", shared);
        }
    }
}
#endif
