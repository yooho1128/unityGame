#if UNITY_EDITOR
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>최종막 완성 초상과 필드 2프레임을 실제 진행 없이 확인한다.</summary>
    public class FinalShadowArtPreviewWindow : EditorWindow
    {
        private static readonly PreviewSpec[] Specs =
        {
            new PreviewSpec("inverted_guide", "거꾸로 안내원", "2보 보행 · 등불 흔들림"),
            new PreviewSpec("silent_applause", "침묵의 박수", "부유 · 손 펼침/합장"),
            new PreviewSpec("outside_script_shadow", "각본 밖의 그림자", "장막 팽창 · 위상 이동")
        };

        private Vector2 _scroll;

        [MenuItem("Tools/Shadow Theater/Preview Final Shadow Art")]
        private static void Open()
        {
            var window = GetWindow<FinalShadowArtPreviewWindow>("최종 그림자 미리보기");
            window.minSize = new Vector2(900f, 620f);
            window.Show();
        }

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("최종막 완성 그림자", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("큰 이미지는 전투·각본집 초상, 아래 이미지는 게임에서 반복되는 필드 2프레임입니다.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            foreach (PreviewSpec spec in Specs) DrawSpec(spec);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSpec(PreviewSpec spec)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(280f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(spec.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(spec.motionLabel, EditorStyles.miniLabel);

            Texture2D portrait = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Final/Portraits/{spec.id}.png");
            Rect portraitRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.Height(330f));
            if (portrait != null) EditorGUI.DrawPreviewTexture(portraitRect, portrait, null, ScaleMode.ScaleToFit);
            else EditorGUI.HelpBox(portraitRect, "초상 PNG를 찾을 수 없습니다.", MessageType.Error);

            int frame = Mathf.FloorToInt((float)EditorApplication.timeSinceStartup * 5f) & 1;
            Texture2D field = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Final/Field/{spec.id}_{frame + 1:00}.png");
            Rect fieldRect = GUILayoutUtility.GetRect(120f, 150f, GUILayout.ExpandWidth(true));
            if (field != null) EditorGUI.DrawPreviewTexture(fieldRect, field, null, ScaleMode.ScaleToFit);

            if (GUILayout.Button("생성된 ShadowData 선택"))
            {
                var data = AssetDatabase.LoadAssetAtPath<ShadowData>($"Assets/Data/Generated/Shadows/{spec.id}.asset");
                if (data != null) Selection.activeObject = data;
                else Debug.LogWarning($"[Shadow Art] {spec.id} 데이터가 없습니다. Generate Playable Prologue를 먼저 실행하세요.");
            }
            EditorGUILayout.EndVertical();
        }

        private readonly struct PreviewSpec
        {
            public readonly string id;
            public readonly string displayName;
            public readonly string motionLabel;

            public PreviewSpec(string id, string displayName, string motionLabel)
            {
                this.id = id;
                this.displayName = displayName;
                this.motionLabel = motionLabel;
            }
        }
    }
}
#endif
