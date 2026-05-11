using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using ApiTestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;

namespace UnityToolbox.TestCenter
{
    public sealed class ReusableTestCenterRunner
    {
        private const string NoneText = "None";
        private const string IdleStatusText = "Idle";
        private const string NoRunsYetText = "No runs yet.";
        private const string NoResultFileText = "Not created.";
        private const string NoFailedTestsText = "None.";

        private readonly ReusableTestCenterDefinition _definition;
        private readonly ReusableTestSuiteDefinition[] _allSuites;
        private readonly TestRunnerApi _api;
        private readonly CallbacksProxy _callbacks;
        private readonly List<ReusableTestSuiteDefinition> _pendingSequentialSuites = new();

        private readonly string _currentSuiteSessionKey;
        private readonly string _statusSessionKey;
        private readonly string _lastResultSummarySessionKey;
        private readonly string _lastResultFilePathSessionKey;
        private readonly string _pendingResultRelativePathSessionKey;
        private readonly string _pendingResultFilePathSessionKey;
        private readonly string _runActiveSessionKey;
        private readonly string _lastFailedTestsSessionKey;
        private readonly string _sequentialActiveSessionKey;
        private readonly string _sequentialQueueSessionKey;
        private readonly string _sequentialTotalSessionKey;
        private readonly string _sequentialCompletedSessionKey;

        private string _currentSuiteName = NoneText;
        private string _statusText = IdleStatusText;
        private string _lastResultSummary = NoRunsYetText;
        private string _lastResultFilePath = NoResultFileText;
        private string _pendingResultRelativePath = string.Empty;
        private string _pendingResultFilePath = string.Empty;
        private string _lastFailedTests = NoFailedTestsText;
        private bool _sequentialRunActive;
        private int _sequentialTotal;
        private int _sequentialCompleted;
        private bool _runActive;

        public ReusableTestCenterRunner(
            ReusableTestCenterDefinition definition,
            ReusableTestSuiteDefinition[] allSuites)
        {
            _definition = definition;
            _allSuites = allSuites ?? Array.Empty<ReusableTestSuiteDefinition>();
            ValidateSuiteDefinitions(_allSuites);

            var prefix = definition.SessionStatePrefix;
            _currentSuiteSessionKey = prefix + "CurrentSuite";
            _statusSessionKey = prefix + "Status";
            _lastResultSummarySessionKey = prefix + "LastResultSummary";
            _lastResultFilePathSessionKey = prefix + "LastResultFilePath";
            _pendingResultRelativePathSessionKey = prefix + "PendingResultRelativePath";
            _pendingResultFilePathSessionKey = prefix + "PendingResultFilePath";
            _runActiveSessionKey = prefix + "RunActive";
            _lastFailedTestsSessionKey = prefix + "LastFailedTests";
            _sequentialActiveSessionKey = prefix + "SequentialActive";
            _sequentialQueueSessionKey = prefix + "SequentialQueue";
            _sequentialTotalSessionKey = prefix + "SequentialTotal";
            _sequentialCompletedSessionKey = prefix + "SequentialCompleted";

            LoadPersistedState();

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new CallbacksProxy(this);
            _api.RegisterCallbacks(_callbacks);

            if (_sequentialRunActive && _pendingSequentialSuites.Count > 0 && !_runActive)
                ScheduleNextSequentialSuite();
        }

        public event Action StateChanged;

        public string EditModeLogPath => ResolvePreferredLogPath("EditMode", "EditMode.current");

        public string PlayModeLogPath => ResolvePreferredLogPath("PlayMode", "PlayMode.current");

        public string TestResultsDirectory => Path.Combine(GetProjectRoot(), _definition.ResultsDirectoryRelativePath);

        public string CurrentSuiteName => _currentSuiteName;

        public string StatusText => _statusText;

        public string LastResultSummary => _lastResultSummary;

        public string LastResultFilePath => _lastResultFilePath;

        public string LastFailedTests => _lastFailedTests;

        public bool HasPendingSequentialSuites => _sequentialRunActive && _pendingSequentialSuites.Count > 0;

        public bool CanRun => !_runActive && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;

        public void EnsureInitialized()
        {
        }

        public void RunSuite(ReusableTestSuiteDefinition suite)
        {
            EnsureInitialized();
            if (!CanRun)
            {
                TryRunSuite(suite, showBlockedDialog: true);
                return;
            }

            ClearSequentialState();
            TryRunSuite(suite, showBlockedDialog: true);
        }

        public void RunSuitesSequentially(IReadOnlyList<ReusableTestSuiteDefinition> suites)
        {
            EnsureInitialized();

            if (!CanRun)
            {
                EditorUtility.DisplayDialog(
                    _definition.WindowTitle,
                    "A test run is already active, Unity is compiling, or the editor is changing play mode.",
                    "OK");
                return;
            }

            if (suites == null || suites.Count == 0)
            {
                EditorUtility.DisplayDialog(_definition.WindowTitle, "Select at least one test suite before running a queue.", "OK");
                return;
            }

            _pendingSequentialSuites.Clear();
            for (var i = 0; i < suites.Count; i++)
            {
                if (!ContainsSuite(_pendingSequentialSuites, suites[i]))
                    _pendingSequentialSuites.Add(suites[i]);
            }

            if (_pendingSequentialSuites.Count == 0)
                return;

            _sequentialRunActive = true;
            _sequentialCompleted = 0;
            _sequentialTotal = _pendingSequentialSuites.Count;
            PersistState();
            StartNextSequentialSuiteOrFinish();
        }

        public void ClearPendingSequentialSuites()
        {
            _pendingSequentialSuites.Clear();
            _sequentialRunActive = false;
            _sequentialTotal = 0;
            _sequentialCompleted = 0;
            PersistState();
            NotifyStateChanged();
        }

        public bool SuiteEquals(ReusableTestSuiteDefinition left, ReusableTestSuiteDefinition right)
        {
            return string.Equals(GetSuiteKey(left), GetSuiteKey(right), StringComparison.Ordinal);
        }

        public void RevealResultsDirectory()
        {
            Directory.CreateDirectory(TestResultsDirectory);
            EditorUtility.RevealInFinder(TestResultsDirectory);
        }

        public bool ConfirmAndClearLogs()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Test Logs",
                    "This will permanently delete all saved test log files in the results directory.\n\nXML result files will be kept.",
                    "Yes, clear all logs",
                    "Cancel"))
            {
                return false;
            }

            Directory.CreateDirectory(TestResultsDirectory);

            foreach (var logFile in Directory.GetFiles(TestResultsDirectory, "*.log", SearchOption.TopDirectoryOnly))
                File.Delete(logFile);

            foreach (var markerFile in Directory.GetFiles(TestResultsDirectory, "*.current", SearchOption.TopDirectoryOnly))
                File.Delete(markerFile);

            NotifyStateChanged();
            return true;
        }

        public string ReadLogTail(string path, int maxCharacters = 6000)
        {
            if (!File.Exists(path))
                return "Log file has not been created yet.";

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            if (content.Length <= maxCharacters)
                return content;

            return "...\n" + content.Substring(content.Length - maxCharacters, maxCharacters);
        }

        private static void ValidateSuiteDefinitions(IReadOnlyList<ReusableTestSuiteDefinition> suites)
        {
            var knownKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < suites.Count; i++)
            {
                var key = GetSuiteKey(suites[i]);
                if (!knownKeys.Add(key))
                    throw new InvalidOperationException($"Duplicate test suite key detected: {key}");
            }
        }

        private bool TryRunSuite(ReusableTestSuiteDefinition suite, bool showBlockedDialog)
        {
            if (!CanRun)
            {
                if (showBlockedDialog)
                {
                    EditorUtility.DisplayDialog(
                        _definition.WindowTitle,
                        "A test run is already active, Unity is compiling, or the editor is changing play mode.",
                        "OK");
                }

                return false;
            }

            Directory.CreateDirectory(TestResultsDirectory);
            PrepareLogFileForRun(suite);

            if (_sequentialRunActive && _sequentialTotal > 0)
            {
                var sequenceIndex = Mathf.Clamp(_sequentialCompleted + 1, 1, _sequentialTotal);
                _currentSuiteName = $"[{sequenceIndex}/{_sequentialTotal}] {suite.DisplayName}";
                _statusText = $"Queued sequence {sequenceIndex}/{_sequentialTotal}";
            }
            else
            {
                _currentSuiteName = suite.DisplayName;
                _statusText = "Queued";
            }

            _pendingResultRelativePath = Path.Combine(_definition.ResultsDirectoryRelativePath, suite.ResultFileName);
            _pendingResultFilePath = Path.Combine(TestResultsDirectory, suite.ResultFileName);
            _lastFailedTests = NoFailedTestsText;
            PersistState();
            NotifyStateChanged();

            var filter = new Filter
            {
                testMode = suite.Mode,
                categoryNames = new[] { suite.CategoryName }
            };

            _api.Execute(new ExecutionSettings(filter));
            return true;
        }

        private void StartNextSequentialSuiteOrFinish()
        {
            if (!_sequentialRunActive)
                return;

            if (_pendingSequentialSuites.Count == 0)
            {
                FinishSequentialRun();
                return;
            }

            if (!CanRun)
            {
                ScheduleNextSequentialSuite();
                return;
            }

            var suite = _pendingSequentialSuites[0];
            _pendingSequentialSuites.RemoveAt(0);
            PersistState();

            if (!TryRunSuite(suite, showBlockedDialog: false))
            {
                _pendingSequentialSuites.Insert(0, suite);
                PersistState();
                ScheduleNextSequentialSuite();
            }
        }

        private void ScheduleNextSequentialSuite()
        {
            EditorApplication.delayCall -= TryStartNextSequentialSuiteAfterDelay;
            EditorApplication.delayCall += TryStartNextSequentialSuiteAfterDelay;
        }

        private void TryStartNextSequentialSuiteAfterDelay()
        {
            EditorApplication.delayCall -= TryStartNextSequentialSuiteAfterDelay;

            if (!_sequentialRunActive)
                return;

            if (!CanRun)
            {
                ScheduleNextSequentialSuite();
                return;
            }

            StartNextSequentialSuiteOrFinish();
        }

        private void FinishSequentialRun()
        {
            var completed = _sequentialCompleted;
            var total = _sequentialTotal;
            ClearSequentialState();
            _statusText = IdleStatusText;
            if (total > 0)
                _lastResultSummary = $"Queue complete: {completed}/{total} suite(s) finished. Last result: {_lastResultSummary}";

            PersistState();
            NotifyStateChanged();
        }

        private void ClearSequentialState()
        {
            _pendingSequentialSuites.Clear();
            _sequentialRunActive = false;
            _sequentialTotal = 0;
            _sequentialCompleted = 0;
            PersistState();
        }

        private string GetProjectRoot()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            return root ?? throw new InvalidOperationException("Could not resolve Unity project root.");
        }

        private void PrepareLogFileForRun(ReusableTestSuiteDefinition suite)
        {
            var modePrefix = suite.Mode == ApiTestMode.EditMode ? "EditMode" : "PlayMode";
            var markerFileName = suite.Mode == ApiTestMode.EditMode ? "EditMode.current" : "PlayMode.current";
            var logFileName = $"{modePrefix}_{SanitizeFileNamePart(suite.DisplayName)}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            var logPath = Path.Combine(TestResultsDirectory, logFileName);

            File.WriteAllText(Path.Combine(TestResultsDirectory, markerFileName), logPath);

            if (!File.Exists(logPath))
                File.WriteAllText(logPath, string.Empty);
        }

        private string ResolvePreferredLogPath(string prefix, string markerFileName)
        {
            Directory.CreateDirectory(TestResultsDirectory);

            var markerPath = Path.Combine(TestResultsDirectory, markerFileName);
            if (File.Exists(markerPath))
            {
                var markerTarget = File.ReadAllText(markerPath).Trim();
                if (!string.IsNullOrWhiteSpace(markerTarget))
                    return markerTarget;
            }

            var matchingLogs = Directory.GetFiles(TestResultsDirectory, $"{prefix}_*.log", SearchOption.TopDirectoryOnly);
            if (matchingLogs.Length == 0)
                return Path.Combine(TestResultsDirectory, $"{prefix}.log");

            Array.Sort(matchingLogs, (left, right) => File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));
            return matchingLogs[0];
        }

        private static string SanitizeFileNamePart(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = value;

            foreach (var invalidChar in invalidChars)
                sanitized = sanitized.Replace(invalidChar, '_');

            sanitized = sanitized.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "Run" : sanitized;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void SetRunStarted(ITestAdaptor testsToRun)
        {
            _runActive = true;
            _statusText = _sequentialRunActive && _sequentialTotal > 0
                ? $"Running sequence {Mathf.Clamp(_sequentialCompleted + 1, 1, _sequentialTotal)}/{_sequentialTotal} ({testsToRun.TestCaseCount} tests)"
                : $"Running ({testsToRun.TestCaseCount} tests)";
            PersistState();
            NotifyStateChanged();
        }

        private void SetRunFinished(ITestResultAdaptor result)
        {
            _runActive = false;
            _statusText = IdleStatusText;
            _lastResultSummary =
                $"{result.TestStatus}: Passed {result.PassCount}, Failed {result.FailCount}, Skipped {result.SkipCount}, Inconclusive {result.InconclusiveCount}";

            if (!string.IsNullOrEmpty(_pendingResultFilePath))
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, _pendingResultRelativePath);
                    _lastResultFilePath = _pendingResultFilePath;
                }
                catch (Exception exception)
                {
                    _lastResultFilePath = $"Save failed: {exception.GetType().Name}";
                }

                _pendingResultRelativePath = string.Empty;
                _pendingResultFilePath = string.Empty;
            }

            PersistState();
            NotifyStateChanged();

            if (_sequentialRunActive)
            {
                _sequentialCompleted = Mathf.Clamp(_sequentialCompleted + 1, 0, Math.Max(0, _sequentialTotal));
                if (_pendingSequentialSuites.Count > 0)
                {
                    _statusText = $"Waiting next suite ({_sequentialCompleted}/{_sequentialTotal} complete)";
                    PersistState();
                    NotifyStateChanged();
                    ScheduleNextSequentialSuite();
                }
                else
                {
                    FinishSequentialRun();
                }
            }
        }

        private void AppendFailedTest(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return;

            if (string.Equals(_lastFailedTests, NoFailedTestsText, StringComparison.Ordinal))
            {
                _lastFailedTests = fullName;
                return;
            }

            var existing = _lastFailedTests.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < existing.Length; i++)
            {
                if (string.Equals(existing[i].Trim(), fullName, StringComparison.Ordinal))
                    return;
            }

            _lastFailedTests += Environment.NewLine + fullName;
        }

        private void PersistState()
        {
            SessionState.SetString(_currentSuiteSessionKey, _currentSuiteName ?? NoneText);
            SessionState.SetString(_statusSessionKey, _statusText ?? IdleStatusText);
            SessionState.SetString(_lastResultSummarySessionKey, _lastResultSummary ?? NoRunsYetText);
            SessionState.SetString(_lastResultFilePathSessionKey, _lastResultFilePath ?? NoResultFileText);
            SessionState.SetString(_pendingResultRelativePathSessionKey, _pendingResultRelativePath ?? string.Empty);
            SessionState.SetString(_pendingResultFilePathSessionKey, _pendingResultFilePath ?? string.Empty);
            SessionState.SetString(_lastFailedTestsSessionKey, _lastFailedTests ?? NoFailedTestsText);
            SessionState.SetBool(_runActiveSessionKey, _runActive);
            SessionState.SetBool(_sequentialActiveSessionKey, _sequentialRunActive);
            SessionState.SetString(_sequentialQueueSessionKey, SerializeSuiteQueue(_pendingSequentialSuites));
            SessionState.SetInt(_sequentialTotalSessionKey, _sequentialTotal);
            SessionState.SetInt(_sequentialCompletedSessionKey, _sequentialCompleted);
        }

        private void LoadPersistedState()
        {
            _currentSuiteName = SessionState.GetString(_currentSuiteSessionKey, NoneText);
            _statusText = SessionState.GetString(_statusSessionKey, IdleStatusText);
            _lastResultSummary = SessionState.GetString(_lastResultSummarySessionKey, NoRunsYetText);
            _lastResultFilePath = SessionState.GetString(_lastResultFilePathSessionKey, NoResultFileText);
            _pendingResultRelativePath = SessionState.GetString(_pendingResultRelativePathSessionKey, string.Empty);
            _pendingResultFilePath = SessionState.GetString(_pendingResultFilePathSessionKey, string.Empty);
            _lastFailedTests = SessionState.GetString(_lastFailedTestsSessionKey, NoFailedTestsText);
            _runActive = SessionState.GetBool(_runActiveSessionKey, false);
            _sequentialRunActive = SessionState.GetBool(_sequentialActiveSessionKey, false);
            _sequentialTotal = SessionState.GetInt(_sequentialTotalSessionKey, 0);
            _sequentialCompleted = SessionState.GetInt(_sequentialCompletedSessionKey, 0);
            LoadSuiteQueue(SessionState.GetString(_sequentialQueueSessionKey, string.Empty));
        }

        private string SerializeSuiteQueue(IReadOnlyList<ReusableTestSuiteDefinition> suites)
        {
            if (suites == null || suites.Count == 0)
                return string.Empty;

            var keys = new string[suites.Count];
            for (var i = 0; i < suites.Count; i++)
                keys[i] = GetSuiteKey(suites[i]);

            return string.Join("\n", keys);
        }

        private void LoadSuiteQueue(string serializedQueue)
        {
            _pendingSequentialSuites.Clear();
            if (string.IsNullOrWhiteSpace(serializedQueue))
                return;

            var keys = serializedQueue.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < keys.Length; i++)
            {
                if (TryFindSuiteByKey(keys[i].Trim(), out var suite))
                    _pendingSequentialSuites.Add(suite);
            }
        }

        private bool ContainsSuite(IReadOnlyList<ReusableTestSuiteDefinition> suites, ReusableTestSuiteDefinition target)
        {
            if (suites == null)
                return false;

            for (var i = 0; i < suites.Count; i++)
            {
                if (SuiteEquals(suites[i], target))
                    return true;
            }

            return false;
        }

        private bool TryFindSuiteByKey(string key, out ReusableTestSuiteDefinition suite)
        {
            for (var i = 0; i < _allSuites.Length; i++)
            {
                var candidate = _allSuites[i];
                if (!string.Equals(GetSuiteKey(candidate), key, StringComparison.Ordinal))
                    continue;

                suite = candidate;
                return true;
            }

            suite = default;
            return false;
        }

        private static string GetSuiteKey(ReusableTestSuiteDefinition suite)
        {
            return string.Concat(
                suite.Mode,
                "|",
                suite.CategoryName ?? string.Empty,
                "|",
                suite.ResultFileName ?? string.Empty);
        }

        private sealed class CallbacksProxy : ICallbacks
        {
            private readonly ReusableTestCenterRunner _owner;

            public CallbacksProxy(ReusableTestCenterRunner owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _owner.SetRunStarted(testsToRun);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _owner.SetRunFinished(result);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    _owner.AppendFailedTest(result.FullName);
                    _owner._lastResultSummary = $"Failed: {result.FullName}";
                    _owner.PersistState();
                    _owner.NotifyStateChanged();
                }
            }
        }
    }
}
