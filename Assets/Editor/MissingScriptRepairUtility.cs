#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.EditorTools
{
    /// <summary>생성된 프리팹과 씬에 남은 Missing MonoBehaviour를 찾아 제거한다.</summary>
    public static class MissingScriptRepairUtility
    {
        [MenuItem("Tools/Shadow Theater/Repair Missing Scripts")]
        public static void RepairGeneratedPrefabs()
        {
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;
                try
                {
                    int removed = RemoveFromHierarchy(root, path);
                    if (removed <= 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    total += removed;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            if (total > 0) Debug.LogWarning($"[MissingScriptRepair] 생성 프리팹에서 {total}개를 제거했습니다.");
            else Debug.Log("[MissingScriptRepair] 생성 프리팹 검사 완료: 누락 컴포넌트 없음");
        }

        public static int RemoveFromScene(Scene scene, string context)
        {
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects()) removed += RemoveFromHierarchy(root, context);
            return removed;
        }

        private static int RemoveFromHierarchy(GameObject root, string context)
        {
            int removed = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject target = child.gameObject;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
                if (missing <= 0) continue;
                Debug.LogWarning($"[MissingScriptRepair] {context} :: {HierarchyPath(child)} 에서 {missing}개 제거");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                removed += missing;
            }
            return removed;
        }

        private static string HierarchyPath(Transform target)
        {
            var names = new List<string>();
            while (target != null)
            {
                names.Add(target.name);
                target = target.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
#endif
