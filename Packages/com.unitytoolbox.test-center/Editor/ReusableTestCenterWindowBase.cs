using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityToolbox.TestCenter
{
    public abstract class ReusableTestCenterWindowBase : EditorWindow
    {
        private static GUIStyle _logContentStyle;

        private readonly List<ReusableTestSuiteDefinition> _selectedSuites = new();
        private ReusableTestCenterRunner _boundRunner;
        private ReusableTestCenterRunner _failedRunner;
        private Vector2 _windowScroll;
        private Vector2 _suiteScroll;
        private Vector2 _selectedSuiteScroll;
        private Vector2 _editLogScroll;
        private Vector2 _playLogScroll;
        private string _runnerInitializationError = string.Empty;
        private string _suiteSearchText = string.Empty;
        private string _editLogPreview = string.Empty;
        private string _playLogPreview = string.Empty;
        private int _visibleSuiteCount;

        protected abstract ReusableTestCenterDefinition Definition { get; }

        protected abstract ReusableTestCenterRunner Runner { get; }

        protected abstract IReadOnlyList<ReusableTestSuiteSection> Sections { get; }

        protected virtual bool HasSuiteConfiguration => true;

        protected virtual bool HasSuiteEntries
        {
            get
            {
                for (var sectionIndex = 0; sectionIndex < Sections.Count; sectionIndex++)
                {
                    if (Sections[sectionIndex].Entries.Length > 0)
                        return true;
                }

                return false;
            }
        }

        protected static void OpenWindow<TWindow>(ReusableTestCenterDefinition definition)
            where TWindow : ReusableTestCenterWindowBase
        {
            var window = GetWindow<TWindow>(definition.WindowTitle);
            window.minSize = new Vector2(definition.MinWidth, definition.MinHeight);
            window.Show();
        }

        protected virtual void OnEnable()
        {
            RefreshRunnerBinding();
            RefreshLogPreview();
        }

        protected virtual void OnDisable()
        {
            UnbindRunner();
        }

        protected virtual void OnGUI()
        {
            RefreshRunnerBinding();
            DrawToolbar();

            if (_boundRunner == null)
            {
                DrawRunnerUnavailable();
                return;
            }

            _windowScroll = EditorGUILayout.BeginScrollView(
                _windowScroll,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(6f);
            DrawStatus();

            EditorGUILayout.Space(10f);
            DrawSuites();

            EditorGUILayout.Space(10f);
            DrawLogs();

            EditorGUILayout.Space(10f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            var runner = _boundRunner;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Definition.ToolbarTitle, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                DrawToolbarButtons();

                if (GUILayout.Button("Refresh Logs", EditorStyles.toolbarButton))
                    RefreshLogPreview();

                if (GUILayout.Button("Open Test Runner", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

                using (new EditorGUI.DisabledScope(runner == null))
                {
                    if (GUILayout.Button("Reveal Results", EditorStyles.toolbarButton))
                        runner.RevealResultsDirectory();
                }

                using (new EditorGUI.DisabledScope(runner == null))
                {
                    if (GUILayout.Button("Clear Logs", EditorStyles.toolbarButton) &&
                        runner.ConfirmAndClearLogs())
                    {
                        RefreshLogPreview();
                    }
                }
            }
        }

        protected virtual void DrawToolbarButtons()
        {
        }

        private void DrawStatus()
        {
            if (_boundRunner == null)
                return;

            EditorGUILayout.LabelField("Run Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusMessages();
                EditorGUILayout.LabelField("State", _boundRunner.StatusText);
                EditorGUILayout.LabelField("Current Suite", _boundRunner.CurrentSuiteName);
                EditorGUILayout.LabelField("Last Result", _boundRunner.LastResultSummary);
                EditorGUILayout.LabelField("Last XML", _boundRunner.LastResultFilePath);
                EditorGUILayout.LabelField("Last Failed Tests", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    _boundRunner.LastFailedTests,
                    EditorStyles.textArea,
                    GUILayout.MinHeight(54f));
            }
        }

        protected virtual void DrawStatusMessages()
        {
        }

        private void DrawSuites()
        {
            if (_boundRunner == null)
                return;

            var searchTokens = GetSuiteSearchTokens();

            EditorGUILayout.LabelField("Suites", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!HasSuiteConfiguration)
                {
                    DrawMissingSuiteConfiguration();
                    return;
                }

                if (!HasSuiteEntries)
                {
                    DrawEmptySuiteConfiguration();
                    return;
                }

                DrawSuiteSearchField(searchTokens);
                DrawSelectedSuitesQueue();
                _visibleSuiteCount = 0;

                _suiteScroll = EditorGUILayout.BeginScrollView(
                    _suiteScroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.MaxHeight(320f),
                    GUILayout.ExpandHeight(false));

                for (var sectionIndex = 0; sectionIndex < Sections.Count; sectionIndex++)
                {
                    var section = Sections[sectionIndex];
                    EditorGUILayout.LabelField(section.Title, EditorStyles.miniBoldLabel);

                    for (var entryIndex = 0; entryIndex < section.Entries.Length; entryIndex++)
                        DrawSuiteButton(section.Entries[entryIndex], searchTokens);

                    if (sectionIndex < Sections.Count - 1)
                        EditorGUILayout.Space(8f);
                }

                if (_visibleSuiteCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No test suites match '{_suiteSearchText}'. Search checks suite name, category, result file, mode, and description.",
                        MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        protected virtual void DrawMissingSuiteConfiguration()
        {
            EditorGUILayout.HelpBox(
                "No suite configuration is available for this window.",
                MessageType.Info);
        }

        protected virtual void DrawEmptySuiteConfiguration()
        {
            EditorGUILayout.HelpBox(
                "Suite configuration is loaded, but no suites are defined yet.",
                MessageType.Info);
        }

        protected void RefreshRunnerBinding()
        {
            var runner = Runner;
            if (runner == null)
            {
                UnbindRunner();
                return;
            }

            if (ReferenceEquals(_boundRunner, runner))
            {
                _runnerInitializationError = string.Empty;
                _failedRunner = null;
                return;
            }

            if (ReferenceEquals(_failedRunner, runner))
                return;

            UnbindRunner();

            try
            {
                runner.EnsureInitialized();
                runner.StateChanged += OnStateChanged;
                _boundRunner = runner;
                _failedRunner = null;
                _runnerInitializationError = string.Empty;
            }
            catch (Exception exception)
            {
                _failedRunner = runner;
                _runnerInitializationError = $"Runner initialization failed: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void DrawSuiteSearchField(IReadOnlyList<string> searchTokens)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(48f));

                EditorGUI.BeginChangeCheck();
                var searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
                var nextSearchText = EditorGUILayout.TextField(_suiteSearchText, searchStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    _suiteSearchText = nextSearchText ?? string.Empty;
                    _suiteScroll = Vector2.zero;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_suiteSearchText)))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(58f)))
                    {
                        _suiteSearchText = string.Empty;
                        _suiteScroll = Vector2.zero;
                        GUI.FocusControl(null);
                    }
                }
            }

            if (searchTokens.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"Filtering by {searchTokens.Count} token(s): {string.Join(", ", searchTokens)}",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Search supports multiple names separated by comma, semicolon, pipe, or a new line.", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawSelectedSuitesQueue()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Selected Queue ({_selectedSuites.Count})", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(_selectedSuites.Count == 0 || !_boundRunner.CanRun))
                    {
                        if (GUILayout.Button("Run Selected In Order", GUILayout.Width(160f)))
                            _boundRunner.RunSuitesSequentially(_selectedSuites);
                    }

                    using (new EditorGUI.DisabledScope(_selectedSuites.Count == 0))
                    {
                        if (GUILayout.Button("Clear", GUILayout.Width(58f)))
                            _selectedSuites.Clear();
                    }

                    using (new EditorGUI.DisabledScope(!_boundRunner.HasPendingSequentialSuites))
                    {
                        if (GUILayout.Button("Stop Pending", GUILayout.Width(96f)))
                            _boundRunner.ClearPendingSequentialSuites();
                    }
                }

                if (_selectedSuites.Count == 0)
                {
                    EditorGUILayout.HelpBox("Add suites below, then run them sequentially in the selected order.", MessageType.Info);
                    return;
                }

                _selectedSuiteScroll = EditorGUILayout.BeginScrollView(
                    _selectedSuiteScroll,
                    GUILayout.MinHeight(58f),
                    GUILayout.MaxHeight(118f),
                    GUILayout.ExpandHeight(false));

                for (var i = 0; i < _selectedSuites.Count; i++)
                    DrawSelectedSuiteRow(i);

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawSelectedSuiteRow(int index)
        {
            var suite = _selectedSuites[index];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{index + 1}. {suite.DisplayName}", GUILayout.Width(260f));
                EditorGUILayout.LabelField(suite.Mode.ToString(), GUILayout.Width(72f));
                EditorGUILayout.LabelField(suite.CategoryName, GUILayout.MinWidth(140f));

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(42f)))
                    {
                        (_selectedSuites[index - 1], _selectedSuites[index]) = (_selectedSuites[index], _selectedSuites[index - 1]);
                        GUI.FocusControl(null);
                    }
                }

                using (new EditorGUI.DisabledScope(index >= _selectedSuites.Count - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(52f)))
                    {
                        (_selectedSuites[index + 1], _selectedSuites[index]) = (_selectedSuites[index], _selectedSuites[index + 1]);
                        GUI.FocusControl(null);
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(68f)))
                {
                    _selectedSuites.RemoveAt(index);
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawSuiteButton(ReusableTestSuiteEntry entry, IReadOnlyList<string> searchTokens)
        {
            if (!SuiteMatchesSearch(entry, searchTokens))
                return;

            _visibleSuiteCount++;

            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = IsSuiteSelected(entry.Suite);
                if (GUILayout.Button(selected ? "Remove" : "Add", GUILayout.Width(68f)))
                {
                    if (selected)
                        RemoveSelectedSuite(entry.Suite);
                    else
                        _selectedSuites.Add(entry.Suite);

                    GUI.FocusControl(null);
                }

                using (new EditorGUI.DisabledScope(!_boundRunner.CanRun))
                {
                    if (GUILayout.Button(entry.Suite.DisplayName, GUILayout.Width(220f)))
                        _boundRunner.RunSuite(entry.Suite);
                }

                EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(2f);
        }

        private static bool SuiteMatchesSearch(ReusableTestSuiteEntry entry, IReadOnlyList<string> searchTokens)
        {
            if (searchTokens.Count == 0)
                return true;

            for (var i = 0; i < searchTokens.Count; i++)
            {
                var query = searchTokens[i];
                if (ContainsSearchText(entry.Suite.DisplayName, query) ||
                    ContainsSearchText(entry.Suite.CategoryName, query) ||
                    ContainsSearchText(entry.Suite.ResultFileName, query) ||
                    ContainsSearchText(entry.Suite.Mode.ToString(), query) ||
                    ContainsSearchText(entry.Description, query))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] GetSuiteSearchTokens()
        {
            if (string.IsNullOrWhiteSpace(_suiteSearchText))
                return Array.Empty<string>();

            return _suiteSearchText
                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private bool IsSuiteSelected(ReusableTestSuiteDefinition suite)
        {
            if (_boundRunner == null)
                return false;

            for (var i = 0; i < _selectedSuites.Count; i++)
            {
                if (_boundRunner.SuiteEquals(_selectedSuites[i], suite))
                    return true;
            }

            return false;
        }

        private void RemoveSelectedSuite(ReusableTestSuiteDefinition suite)
        {
            if (_boundRunner == null)
                return;

            for (var i = _selectedSuites.Count - 1; i >= 0; i--)
            {
                if (_boundRunner.SuiteEquals(_selectedSuites[i], suite))
                    _selectedSuites.RemoveAt(i);
            }
        }

        private static bool ContainsSearchText(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawLogs()
        {
            if (_boundRunner == null)
                return;

            EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLogPanel(
                    "EditMode Log",
                    _boundRunner.EditModeLogPath,
                    ref _editLogScroll,
                    _editLogPreview);

                DrawLogPanel(
                    "PlayMode Log",
                    _boundRunner.PlayModeLogPath,
                    ref _playLogScroll,
                    _playLogPreview);
            }
        }

        private void DrawLogPanel(string title, string path, ref Vector2 scroll, string content)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(260f)))
            {
                var logContentStyle = GetLogContentStyle();

                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                var contentToShow = string.IsNullOrEmpty(content) ? "Log file has not been created yet." : content;
                var visibleWidth = Mathf.Max(position.width * 0.5f - 70f, 240f);
                var contentSize = MeasureLogContent(contentToShow, visibleWidth, logContentStyle);

                scroll = EditorGUILayout.BeginScrollView(scroll, true, true, GUILayout.MinHeight(220f), GUILayout.ExpandHeight(true));
                var rect = GUILayoutUtility.GetRect(contentSize.x, contentSize.y, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                EditorGUI.SelectableLabel(rect, contentToShow, logContentStyle);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRunnerUnavailable()
        {
            EditorGUILayout.Space(8f);
            DrawStatusMessages();

            var message = string.IsNullOrWhiteSpace(_runnerInitializationError)
                ? "Test Center runner is not available. Reload the configuration or reopen the window."
                : _runnerInitializationError;

            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private void RefreshLogPreview()
        {
            if (_boundRunner == null)
            {
                _editLogPreview = "Runner is not initialized.";
                _playLogPreview = "Runner is not initialized.";
                Repaint();
                return;
            }

            _editLogPreview = _boundRunner.ReadLogTail(_boundRunner.EditModeLogPath);
            _playLogPreview = _boundRunner.ReadLogTail(_boundRunner.PlayModeLogPath);
            Repaint();
        }

        private void OnStateChanged()
        {
            RefreshLogPreview();
        }

        private void UnbindRunner()
        {
            if (_boundRunner == null)
                return;

            _boundRunner.StateChanged -= OnStateChanged;
            _boundRunner = null;
        }

        private static GUIStyle GetLogContentStyle()
        {
            _logContentStyle ??= CreateLogContentStyle();
            return _logContentStyle;
        }

        private static GUIStyle CreateLogContentStyle()
        {
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                richText = false
            };

            return style;
        }

        private static Vector2 MeasureLogContent(string content, float minimumWidth, GUIStyle logContentStyle)
        {
            var lines = content.Replace("\r\n", "\n").Split('\n');
            var maxWidth = minimumWidth;

            for (var i = 0; i < lines.Length; i++)
            {
                var lineWidth = logContentStyle.CalcSize(new GUIContent(lines[i])).x;
                if (lineWidth > maxWidth)
                    maxWidth = lineWidth;
            }

            var height = Mathf.Max(
                220f,
                logContentStyle.CalcHeight(new GUIContent(content), Mathf.Max(minimumWidth, maxWidth)) + 10f);

            return new Vector2(maxWidth + 24f, height);
        }
    }
}
