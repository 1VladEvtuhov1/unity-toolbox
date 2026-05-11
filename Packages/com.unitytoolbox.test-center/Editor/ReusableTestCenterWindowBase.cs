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
        private Vector2 _windowScroll;
        private Vector2 _suiteScroll;
        private Vector2 _selectedSuiteScroll;
        private Vector2 _editLogScroll;
        private Vector2 _playLogScroll;
        private string _suiteSearchText = string.Empty;
        private string _editLogPreview = string.Empty;
        private string _playLogPreview = string.Empty;
        private int _visibleSuiteCount;

        protected abstract ReusableTestCenterDefinition Definition { get; }

        protected abstract ReusableTestCenterRunner Runner { get; }

        protected abstract IReadOnlyList<ReusableTestSuiteSection> Sections { get; }

        protected static void OpenWindow<TWindow>(ReusableTestCenterDefinition definition)
            where TWindow : ReusableTestCenterWindowBase
        {
            var window = GetWindow<TWindow>(definition.WindowTitle);
            window.minSize = new Vector2(definition.MinWidth, definition.MinHeight);
            window.Show();
        }

        protected virtual void OnEnable()
        {
            Runner.EnsureInitialized();
            Runner.StateChanged += OnStateChanged;
            RefreshLogPreview();
        }

        protected virtual void OnDisable()
        {
            Runner.StateChanged -= OnStateChanged;
        }

        protected virtual void OnGUI()
        {
            DrawToolbar();

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
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Definition.ToolbarTitle, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh Logs", EditorStyles.toolbarButton))
                    RefreshLogPreview();

                if (GUILayout.Button("Open Test Runner", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

                if (GUILayout.Button("Reveal Results", EditorStyles.toolbarButton))
                    Runner.RevealResultsDirectory();

                if (GUILayout.Button("Clear Logs", EditorStyles.toolbarButton))
                {
                    if (Runner.ConfirmAndClearLogs())
                        RefreshLogPreview();
                }
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Run Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State", Runner.StatusText);
                EditorGUILayout.LabelField("Current Suite", Runner.CurrentSuiteName);
                EditorGUILayout.LabelField("Last Result", Runner.LastResultSummary);
                EditorGUILayout.LabelField("Last XML", Runner.LastResultFilePath);
                EditorGUILayout.LabelField("Last Failed Tests", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    Runner.LastFailedTests,
                    EditorStyles.textArea,
                    GUILayout.MinHeight(54f));
            }
        }

        private void DrawSuites()
        {
            var searchTokens = GetSuiteSearchTokens();

            EditorGUILayout.LabelField("Suites", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
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

                    using (new EditorGUI.DisabledScope(_selectedSuites.Count == 0 || !Runner.CanRun))
                    {
                        if (GUILayout.Button("Run Selected In Order", GUILayout.Width(160f)))
                            Runner.RunSuitesSequentially(_selectedSuites);
                    }

                    using (new EditorGUI.DisabledScope(_selectedSuites.Count == 0))
                    {
                        if (GUILayout.Button("Clear", GUILayout.Width(58f)))
                            _selectedSuites.Clear();
                    }

                    using (new EditorGUI.DisabledScope(!Runner.HasPendingSequentialSuites))
                    {
                        if (GUILayout.Button("Stop Pending", GUILayout.Width(96f)))
                            Runner.ClearPendingSequentialSuites();
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

                using (new EditorGUI.DisabledScope(!Runner.CanRun))
                {
                    if (GUILayout.Button(entry.Suite.DisplayName, GUILayout.Width(220f)))
                        Runner.RunSuite(entry.Suite);
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
            for (var i = 0; i < _selectedSuites.Count; i++)
            {
                if (Runner.SuiteEquals(_selectedSuites[i], suite))
                    return true;
            }

            return false;
        }

        private void RemoveSelectedSuite(ReusableTestSuiteDefinition suite)
        {
            for (var i = _selectedSuites.Count - 1; i >= 0; i--)
            {
                if (Runner.SuiteEquals(_selectedSuites[i], suite))
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
            EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLogPanel(
                    "EditMode Log",
                    Runner.EditModeLogPath,
                    ref _editLogScroll,
                    _editLogPreview);

                DrawLogPanel(
                    "PlayMode Log",
                    Runner.PlayModeLogPath,
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

        private void RefreshLogPreview()
        {
            _editLogPreview = Runner.ReadLogTail(Runner.EditModeLogPath);
            _playLogPreview = Runner.ReadLogTail(Runner.PlayModeLogPath);
            Repaint();
        }

        private void OnStateChanged()
        {
            RefreshLogPreview();
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
