#if UNITY_EDITOR
using System.IO;
using ShadowTheater.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.EditorTools
{
    /// <summary>Compile and enter Editor Play Mode without building APKs or running regression suites.</summary>
    public static class SourceOperationBatch
    {
        private const string Key = "ShadowTheater.SourceOperation.";
        private const string Report = "Docs/Generated/SourceOperation/UNITY.txt";
        private static double _playStarted = -1;

        public static void RunForCi()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Unity source operation check\n");
            SessionState.SetString(Key + "Company", PlayerSettings.companyName);
            SessionState.SetString(Key + "Product", PlayerSettings.productName);
            // Editor Play Mode must not load or overwrite the user's normal desktop save.
            PlayerSettings.companyName = "ShadowTheaterSourceOperation";
            PlayerSettings.productName = "SourceOperation";
            SessionState.SetInt(Key + "Errors", 0);
            SessionState.SetBool(Key + "Finished", false);
            SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key + "Active", true);
            Attach();
            EditorSceneManager.OpenScene("Assets/Scenes/Prologue/PrologueTheater.unity");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Attach()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Capture; Application.logMessageReceived += Capture;
        }

        private static void Capture(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key + "Active", false) || !Application.isPlaying ||
                type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
            File.AppendAllText(Report, message + "\n" + stack + "\n");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            if (SessionState.GetBool(Key + "Finished", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PlayerSettings.companyName = SessionState.GetString(Key + "Company", "DefaultCompany");
                PlayerSettings.productName = SessionState.GetString(Key + "Product", "Shadow Theater");
                AssetDatabase.SaveAssets();
                int errors = SessionState.GetInt(Key + "Errors", 0);
                SessionState.SetBool(Key + "Active", false);
                File.AppendAllText(Report, $"Play Mode stopped; player build not requested; runtime errors={errors}\n");
                Debug.Log($"[SourceOperation] {(errors == 0 ? "PASS" : "FAIL")}: {Report}");
                EditorApplication.Exit(errors == 0 ? 0 : 1);
                return;
            }
            if (Application.isPlaying)
            {
                if (_playStarted < 0) _playStarted = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - _playStarted < 12) return;
                bool ready = PlayerController.Instance != null && FieldGrid.Current != null && GameFlowController.Instance != null;
                File.AppendAllText(Report, $"Scene={SceneManager.GetActiveScene().name}; Player/Grid/GameFlow initialized={ready}; observed Play Mode for 12 seconds\n");
                if (!ready) SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
                SessionState.SetBool(Key + "Finished", true);
                EditorApplication.ExitPlaymode();
            }
            else if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 120)
            {
                File.AppendAllText(Report, "Play Mode startup timed out\n");
                SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
                SessionState.SetBool(Key + "Finished", true);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
#endif
