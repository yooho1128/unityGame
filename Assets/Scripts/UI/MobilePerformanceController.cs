using System.Collections;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.UI
{
    /// <summary>모바일 기기 등급에 맞춰 프레임·품질·메모리 회수를 한곳에서 관리한다.</summary>
    public class MobilePerformanceController : MonoBehaviour
    {
        [SerializeField, Range(30, 120)] private int standardTargetFps = 60;
        [SerializeField, Range(20, 60)] private int lowMemoryTargetFps = 30;
        [SerializeField, Min(1024)] private int lowSystemMemoryMb = 3072;
        [SerializeField, Min(256)] private int lowGraphicsMemoryMb = 1024;
        [SerializeField, Min(1)] private int unloadEverySceneChanges = 5;
        [SerializeField] private bool applyInEditorForTesting;

        private int _sceneChanges;
        private bool _lowSpec;
        private bool _active;

        private void Awake()
        {
            _active = Application.isMobilePlatform || applyInEditorForTesting;
            if (!_active) return;
            _lowSpec = IsLowSpecDevice();
            ApplyQualityProfile();
        }

        private void OnEnable()
        {
            if (!_active) return;
            Application.lowMemory += OnLowMemory;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Application.lowMemory -= OnLowMemory;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private bool IsLowSpecDevice()
        {
            bool lowRam = SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize <= lowSystemMemoryMb;
            bool lowVram = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= lowGraphicsMemoryMb;
            return lowRam || lowVram;
        }

        private void ApplyQualityProfile()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _lowSpec ? lowMemoryTargetFps : standardTargetFps;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.softParticles = false;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.pixelLightCount = _lowSpec ? 1 : 2;
            QualitySettings.skinWeights = _lowSpec ? SkinWeights.OneBone : SkinWeights.TwoBones;
            Input.multiTouchEnabled = true;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneChanges++;
            if (_sceneChanges % unloadEverySceneChanges == 0) StartCoroutine(ReleaseUnusedAssets(false));
        }

        private void OnLowMemory()
        {
            SaveManager.Instance?.Save();
            StartCoroutine(ReleaseUnusedAssets(true));
        }

        private static IEnumerator ReleaseUnusedAssets(bool collectManagedHeap)
        {
            yield return Resources.UnloadUnusedAssets();
            if (collectManagedHeap) System.GC.Collect();
        }
    }
}
