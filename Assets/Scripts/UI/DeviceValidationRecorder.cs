using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ShadowTheater.Battle;
using ShadowTheater.Field;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.UI
{
    [Serializable]
    public class DeviceValidationReport
    {
        public string reportVersion = "2";
        public string sessionId;
        public string generatedAtUtc;
        public string platform;
        public string deviceModel;
        public string operatingSystem;
        public string graphicsDevice;
        public int systemMemoryMb;
        public int graphicsMemoryMb;
        public string applicationVersion;
        public string buildGuid;
        public bool developmentBuild;
        public int metricsRevision;
        public float sessionSeconds;
        public float averageFps;
        public float minimumOneSecondFps;
        public int frameSamples;
        public float maximumFrameSeconds;
        public int sceneLoads;
        public int uniqueScenes;
        public int battlesCompleted;
        public int savesCompleted;
        public int loadsCompleted;
        public int pauseCount;
        public int resumeCount;
        public int resumesObservedInUpdate;
        public float suspendedSeconds;
        public int lowMemoryEvents;
        public int errorCount;
        public bool passed;
        public List<string> visitedScenes = new List<string>();
        public List<string> battleResults = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> failedCriteria = new List<string>();
    }

    /// <summary>Development Build 실기기 세션의 성능·진행·중단 복귀·오류를 자동 기록한다.</summary>
    public class DeviceValidationRecorder : MonoBehaviour
    {
        public static DeviceValidationRecorder Instance { get; private set; }
        public const string ReportFileName = "device_validation_latest.json";

        [SerializeField] private bool collectInReleaseBuild;
        [SerializeField, Min(10f)] private float autoExportInterval = 30f;
        [SerializeField, Min(60f)] private float requiredSessionSeconds = 1200f;
        [SerializeField, Min(1)] private int requiredSceneLoads = 5;
        [SerializeField, Min(1)] private int requiredBattles = 3;
        [SerializeField, Min(1)] private int requiredSaves = 3;
        [SerializeField, Min(1f)] private float requiredAverageFps = 25f;

        public string ReportPath => Path.Combine(Application.persistentDataPath, ReportFileName);
        private DeviceValidationReport _report;
        private GameFlowController _flow;
        private SaveManager _save;
        private float _sampleSeconds;
        private int _sampleFrames;
        private float _bucketSeconds;
        private int _bucketFrames;
        private float _nextExport;
        private readonly object _errorLock = new object();
        private int _threadErrorCount;
        private DateTime? _pausedAtUtc;
        private bool _awaitingResumeFrame;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (!Debug.isDebugBuild && !Application.isEditor && !collectInReleaseBuild)
            {
                enabled = false;
                return;
            }
            _report = NewReport();
            _nextExport = Time.unscaledTime + autoExportInterval;
        }

        private void OnEnable()
        {
            if (_report == null) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.lowMemory += OnLowMemory;
            Application.logMessageReceivedThreaded += OnLog;
            StartCoroutine(BindNextFrame());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.lowMemory -= OnLowMemory;
            Application.logMessageReceivedThreaded -= OnLog;
            UnbindRuntimeEvents();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Export();
            Instance = null;
        }

        private void Update()
        {
            if (_report == null) return;
            if (_awaitingResumeFrame)
            {
                _awaitingResumeFrame = false;
                _report.resumesObservedInUpdate++;
                // 모바일 복귀 직후 delta에는 백그라운드 체류 시간이 섞일 수 있으므로
                // 실제 활성 프레임 성능 표본에서는 제외한다.
                ExportIfDue();
                return;
            }
            float delta = Time.unscaledDeltaTime;
            if (delta > 0f && !float.IsNaN(delta) && !float.IsInfinity(delta))
            {
                _sampleSeconds += delta; _sampleFrames++;
                _report.frameSamples++;
                if (delta > _report.maximumFrameSeconds) _report.maximumFrameSeconds = delta;
                _bucketSeconds += delta; _bucketFrames++;
                if (_bucketSeconds >= 1f)
                {
                    float fps = _bucketFrames / _bucketSeconds;
                    if (_report.minimumOneSecondFps <= 0f || fps < _report.minimumOneSecondFps)
                        _report.minimumOneSecondFps = fps;
                    _bucketSeconds = 0f; _bucketFrames = 0;
                }
                _report.sessionSeconds += delta;
            }
            ExportIfDue();
        }

        private void ExportIfDue()
        {
            if (Time.unscaledTime >= _nextExport)
            {
                _nextExport = Time.unscaledTime + autoExportInterval;
                Export();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _report.sceneLoads++;
            if (!_report.visitedScenes.Contains(scene.name)) _report.visitedScenes.Add(scene.name);
            StartCoroutine(BindNextFrame());
        }

        private IEnumerator BindNextFrame()
        {
            yield return null;
            UnbindRuntimeEvents();
            _flow = FindObjectOfType<GameFlowController>();
            if (_flow != null) _flow.OnBattleFlowFinished += OnBattleFinished;
            _save = SaveManager.Instance;
            if (_save != null)
            {
                _save.OnSaved += OnSaved;
                _save.OnLoaded += OnLoaded;
            }
        }

        private void UnbindRuntimeEvents()
        {
            if (_flow != null) _flow.OnBattleFlowFinished -= OnBattleFinished;
            if (_save != null) { _save.OnSaved -= OnSaved; _save.OnLoaded -= OnLoaded; }
            _flow = null; _save = null;
        }

        private void OnBattleFinished(BattleContext context, BattleOutcome outcome)
        {
            _report.battlesCompleted++;
            if (outcome != null) _report.battleResults.Add(outcome.result.ToString());
        }
        private void OnSaved() => _report.savesCompleted++;
        private void OnLoaded() => _report.loadsCompleted++;
        private void OnLowMemory() { _report.lowMemoryEvents++; Export(); }

        private void OnApplicationPause(bool paused)
        {
            if (_report == null) return;
            if (paused)
            {
                if (_pausedAtUtc.HasValue) return;
                _pausedAtUtc = DateTime.UtcNow;
                _report.pauseCount++;
                Export();
                return;
            }
            // 일부 플랫폼은 시작 시 pause=false를 보낼 수 있다. 실제 pause와 짝이
            // 맞는 복귀만 세어 QA가 거짓 통과하지 않게 한다.
            if (!_pausedAtUtc.HasValue) return;
            double seconds = (DateTime.UtcNow - _pausedAtUtc.Value).TotalSeconds;
            if (seconds > 0d && seconds <= float.MaxValue)
                _report.suspendedSeconds += (float)seconds;
            _pausedAtUtc = null;
            _report.resumeCount++;
            _awaitingResumeFrame = true;
        }

        private void OnApplicationQuit() => Export();

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Interlocked.Increment(ref _threadErrorCount);
            lock (_errorLock)
            {
                if (_report?.errors != null && _report.errors.Count < 20)
                    _report.errors.Add($"{type}: {condition}");
            }
        }

        public void Export()
        {
            if (_report == null) return;
            string json;
            lock (_errorLock)
            {
                _report.generatedAtUtc = DateTime.UtcNow.ToString("O");
                _report.uniqueScenes = _report.visitedScenes.Count;
                _report.averageFps = _sampleSeconds > 0f ? _sampleFrames / _sampleSeconds : 0f;
                _report.errorCount = _threadErrorCount;
                Evaluate();
                json = JsonUtility.ToJson(_report, true);
            }
            try { File.WriteAllText(ReportPath, json); }
            catch (Exception e) { Debug.LogWarning("[DeviceValidation] 리포트 저장 실패: " + e.Message); }
        }

        private void Evaluate()
        {
            _report.failedCriteria = DeviceValidationPolicy.Evaluate(_report);
            if (_report.sessionSeconds < requiredSessionSeconds) _report.failedCriteria.Add($"플레이 시간 {requiredSessionSeconds / 60f:F0}분 미만");
            if (_report.sceneLoads < requiredSceneLoads) _report.failedCriteria.Add($"씬 이동 {requiredSceneLoads}회 미만");
            if (_report.battlesCompleted < requiredBattles) _report.failedCriteria.Add($"전투 완료 {requiredBattles}회 미만");
            if (_report.savesCompleted < requiredSaves) _report.failedCriteria.Add($"저장 {requiredSaves}회 미만");
            if (_report.pauseCount < 1 || _report.resumeCount < 1) _report.failedCriteria.Add("백그라운드 전환·복귀 미검증");
            if (_report.averageFps < requiredAverageFps) _report.failedCriteria.Add($"평균 FPS {requiredAverageFps:F0} 미만");
            if (_report.lowMemoryEvents > 0) _report.failedCriteria.Add("저메모리 이벤트 발생");
            if (_report.errorCount > 0) _report.failedCriteria.Add("오류/예외 로그 발생");
            _report.passed = _report.failedCriteria.Count == 0;
        }

        private static DeviceValidationReport NewReport() => new DeviceValidationReport
        {
            sessionId = Guid.NewGuid().ToString("N"),
            platform = Application.platform.ToString(),
            deviceModel = SystemInfo.deviceModel,
            operatingSystem = SystemInfo.operatingSystem,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            systemMemoryMb = SystemInfo.systemMemorySize,
            graphicsMemoryMb = SystemInfo.graphicsMemorySize,
            applicationVersion = Application.version,
            buildGuid = Application.buildGUID,
            developmentBuild = Debug.isDebugBuild,
            metricsRevision = 2
        };
    }
}
