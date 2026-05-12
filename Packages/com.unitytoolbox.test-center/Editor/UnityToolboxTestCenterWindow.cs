using System;
using UnityEditor;
using UnityEngine;

namespace UnityToolbox.TestCenter
{
    public sealed class UnityToolboxTestCenterWindow : ReusableTestCenterWindowBase
    {
        private const string MenuPath = "Window/Testing/Unity Toolbox Test Center";
        private const string DefaultConfigurationAssetPath = "Assets/Editor/UnityToolboxTestCenter.asset";

        private static readonly ReusableTestCenterDefinition DefaultDefinition =
            new(
                windowTitle: "Unity Toolbox Test Center",
                toolbarTitle: "Unity Toolbox Test Center",
                sessionStatePrefix: "UnityToolbox.TestCenter.Default.");

        private static readonly ReusableTestSuiteDefinition[] EmptySuites = Array.Empty<ReusableTestSuiteDefinition>();
        private static readonly ReusableTestSuiteSection[] EmptySections = Array.Empty<ReusableTestSuiteSection>();

        private ReusableTestCenterDefinition _definition = DefaultDefinition;
        private ReusableTestCenterRunner _runner;
        private ReusableTestSuiteSection[] _sections = EmptySections;
        private ReusableTestCenterConfigurationAsset _configurationAsset;
        private string _configurationAssetPath = string.Empty;
        private string _configurationStatusText = string.Empty;

        protected override ReusableTestCenterDefinition Definition => _definition;

        protected override ReusableTestCenterRunner Runner => _runner;

        protected override System.Collections.Generic.IReadOnlyList<ReusableTestSuiteSection> Sections => _sections;

        protected override bool HasSuiteConfiguration => _configurationAsset != null;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<UnityToolboxTestCenterWindow>();
            window.ReloadConfigurationAndRepaint();
            window.Show();
        }

        protected override void OnEnable()
        {
            ReloadConfiguration();
            ApplyWindowPresentation();
            base.OnEnable();
        }

        protected override void DrawToolbarButtons()
        {
            if (GUILayout.Button("Reload Config", EditorStyles.toolbarButton))
                ReloadConfigurationAndRepaint();

            if (_configurationAsset != null && GUILayout.Button("Select Config", EditorStyles.toolbarButton))
                SelectConfigurationAsset();
        }

        protected override void DrawStatusMessages()
        {
            if (string.IsNullOrWhiteSpace(_configurationStatusText))
                return;

            EditorGUILayout.HelpBox(_configurationStatusText, MessageType.Warning);
        }

        protected override void DrawMissingSuiteConfiguration()
        {
            EditorGUILayout.HelpBox(
                "No Test Center configuration asset was found. Create one to make the built-in window usable without a project-specific wrapper.",
                MessageType.Info);

            EditorGUILayout.LabelField("Expected asset type", nameof(ReusableTestCenterConfigurationAsset), EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Suggested path", DefaultConfigurationAssetPath, EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrWhiteSpace(_configurationStatusText))
                EditorGUILayout.HelpBox(_configurationStatusText, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Config Asset", GUILayout.Width(160f)))
                    CreateConfigurationAsset();
            }
        }

        protected override void DrawEmptySuiteConfiguration()
        {
            var messageType = string.IsNullOrWhiteSpace(_configurationStatusText) ? MessageType.Info : MessageType.Warning;
            var message = string.IsNullOrWhiteSpace(_configurationStatusText)
                ? "Configuration asset is loaded, but it does not define any suites yet. Add sections and suites in the asset inspector."
                : _configurationStatusText;

            EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.LabelField("Config Asset", _configurationAssetPath, EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Config Asset", GUILayout.Width(160f)))
                    SelectConfigurationAsset();
            }
        }

        private void ReloadConfigurationAndRepaint()
        {
            ReloadConfiguration();
            ApplyWindowPresentation();
            RefreshRunnerBinding();
            Repaint();
        }

        private void ReloadConfiguration()
        {
            var configurationResult = FindConfigurationAsset();
            if (configurationResult.Asset == null)
            {
                ApplyFallbackConfiguration(configurationResult.StatusText);
                return;
            }

            try
            {
                var sections = configurationResult.Asset.CreateSections();
                var definition = configurationResult.Asset.CreateDefinition();
                var suites = configurationResult.Asset.CreateAllSuites();

                _configurationAsset = configurationResult.Asset;
                _configurationAssetPath = configurationResult.AssetPath;
                _configurationStatusText = configurationResult.StatusText;
                _definition = definition;
                _sections = sections;
                _runner = CreateRunner(definition, suites);
            }
            catch (Exception exception)
            {
                _configurationAsset = configurationResult.Asset;
                _configurationAssetPath = configurationResult.AssetPath;
                _configurationStatusText = $"Configuration could not be loaded: {exception.Message}";
                _definition = DefaultDefinition;
                _sections = EmptySections;
                _runner = CreateRunner(DefaultDefinition, EmptySuites);
            }
        }

        private void ApplyFallbackConfiguration(string statusText)
        {
            _configurationAsset = null;
            _configurationAssetPath = string.Empty;
            _configurationStatusText = statusText;
            _definition = DefaultDefinition;
            _sections = EmptySections;
            _runner = CreateRunner(DefaultDefinition, EmptySuites);
        }

        private void ApplyWindowPresentation()
        {
            titleContent = new GUIContent(_definition.WindowTitle);
            minSize = new Vector2(_definition.MinWidth, _definition.MinHeight);
        }

        private void CreateConfigurationAsset()
        {
            EnsureFolderExists("Assets/Editor");

            var asset = CreateInstance<ReusableTestCenterConfigurationAsset>();
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(DefaultConfigurationAssetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _configurationAsset = asset;
            _configurationAssetPath = assetPath;
            _configurationStatusText = string.Empty;

            ReloadConfigurationAndRepaint();
            SelectConfigurationAsset();
        }

        private void SelectConfigurationAsset()
        {
            if (_configurationAsset == null)
                return;

            Selection.activeObject = _configurationAsset;
            EditorGUIUtility.PingObject(_configurationAsset);
            EditorUtility.FocusProjectWindow();
        }

        private static ConfigurationAssetSearchResult FindConfigurationAsset()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(ReusableTestCenterConfigurationAsset)}");
            if (guids == null || guids.Length == 0)
                return new ConfigurationAssetSearchResult(null, string.Empty, string.Empty);

            var assetPaths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
                assetPaths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);

            Array.Sort(assetPaths, StringComparer.Ordinal);

            var assetPath = assetPaths[0];
            var asset = AssetDatabase.LoadAssetAtPath<ReusableTestCenterConfigurationAsset>(assetPath);
            if (asset == null)
                return new ConfigurationAssetSearchResult(null, string.Empty, "Configuration asset search found an invalid asset reference.");

            if (assetPaths.Length == 1)
                return new ConfigurationAssetSearchResult(asset, assetPath, string.Empty);

            var statusText =
                $"Multiple configuration assets were found. Using '{assetPath}'. Keep only one asset to avoid ambiguous setup.";

            return new ConfigurationAssetSearchResult(asset, assetPath, statusText);
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            var normalizedPath = assetFolderPath.Replace('\\', '/');
            var pathParts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length == 0)
                throw new InvalidOperationException("Asset folder path must not be empty.");

            var currentPath = pathParts[0];
            for (var index = 1; index < pathParts.Length; index++)
            {
                var nextPath = $"{currentPath}/{pathParts[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, pathParts[index]);

                currentPath = nextPath;
            }
        }

        private static ReusableTestCenterRunner CreateRunner(
            ReusableTestCenterDefinition definition,
            ReusableTestSuiteDefinition[] suites)
        {
            return new ReusableTestCenterRunner(definition, suites);
        }

        private readonly struct ConfigurationAssetSearchResult
        {
            public ConfigurationAssetSearchResult(
                ReusableTestCenterConfigurationAsset asset,
                string assetPath,
                string statusText)
            {
                Asset = asset;
                AssetPath = assetPath ?? string.Empty;
                StatusText = statusText ?? string.Empty;
            }

            public ReusableTestCenterConfigurationAsset Asset { get; }

            public string AssetPath { get; }

            public string StatusText { get; }
        }
    }
}
