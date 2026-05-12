using System;
using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UnityToolbox.TestCenter
{
    [CreateAssetMenu(
        fileName = "UnityToolboxTestCenter",
        menuName = "Unity Toolbox/Test Center Configuration")]
    public sealed class ReusableTestCenterConfigurationAsset : ScriptableObject
    {
        private const string DefaultWindowTitle = "Unity Toolbox Test Center";
        private const string DefaultSessionStatePrefix = "UnityToolbox.TestCenter.Default.";
        private const string DefaultResultsDirectoryRelativePath = "Temp/TestResults";
        private const float DefaultMinWidth = 780f;
        private const float DefaultMinHeight = 540f;

        [SerializeField] private string _windowTitle = DefaultWindowTitle;
        [SerializeField] private string _toolbarTitle = DefaultWindowTitle;
        [SerializeField] private string _sessionStatePrefix = DefaultSessionStatePrefix;
        [SerializeField] private string _resultsDirectoryRelativePath = DefaultResultsDirectoryRelativePath;
        [SerializeField] private float _minWidth = DefaultMinWidth;
        [SerializeField] private float _minHeight = DefaultMinHeight;
        [SerializeField] private ReusableTestSuiteSectionConfiguration[] _sections =
        {
            new()
        };

        public ReusableTestCenterDefinition CreateDefinition()
        {
            return new ReusableTestCenterDefinition(
                _windowTitle,
                _toolbarTitle,
                _sessionStatePrefix,
                _resultsDirectoryRelativePath,
                _minWidth,
                _minHeight);
        }

        public ReusableTestSuiteSection[] CreateSections()
        {
            if (_sections == null || _sections.Length == 0)
                return Array.Empty<ReusableTestSuiteSection>();

            var sections = new List<ReusableTestSuiteSection>(_sections.Length);
            for (var sectionIndex = 0; sectionIndex < _sections.Length; sectionIndex++)
            {
                var section = _sections[sectionIndex];
                if (section == null)
                    continue;

                sections.Add(section.CreateSection());
            }

            return sections.ToArray();
        }

        public ReusableTestSuiteDefinition[] CreateAllSuites()
        {
            var sections = CreateSections();
            if (sections.Length == 0)
                return Array.Empty<ReusableTestSuiteDefinition>();

            var suites = new List<ReusableTestSuiteDefinition>();
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                var entries = sections[sectionIndex].Entries;
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                    suites.Add(entries[entryIndex].Suite);
            }

            return suites.ToArray();
        }
    }

    [Serializable]
    public sealed class ReusableTestSuiteSectionConfiguration
    {
        [SerializeField] private string _title = "Main Suites";
        [SerializeField] private ReusableTestSuiteConfiguration[] _suites = Array.Empty<ReusableTestSuiteConfiguration>();

        public ReusableTestSuiteSection CreateSection()
        {
            if (_suites == null || _suites.Length == 0)
                return new ReusableTestSuiteSection(_title, Array.Empty<ReusableTestSuiteEntry>());

            var entries = new List<ReusableTestSuiteEntry>();
            for (var suiteIndex = 0; suiteIndex < _suites.Length; suiteIndex++)
            {
                var suite = _suites[suiteIndex];
                if (suite == null)
                    continue;

                entries.Add(suite.CreateEntry());
            }

            return new ReusableTestSuiteSection(_title, entries.ToArray());
        }
    }

    [Serializable]
    public sealed class ReusableTestSuiteConfiguration
    {
        [SerializeField] private string _displayName = "Run Suite";
        [SerializeField] private string _categoryName = "MyCategory";
        [SerializeField] private TestMode _mode = TestMode.EditMode;
        [SerializeField] private string _resultFileName = "SuiteResults.xml";
        [SerializeField] [TextArea(2, 4)] private string _description = string.Empty;

        public ReusableTestSuiteEntry CreateEntry()
        {
            var suite = new ReusableTestSuiteDefinition(
                _displayName,
                _categoryName,
                _mode,
                _resultFileName);

            return new ReusableTestSuiteEntry(suite, _description);
        }
    }
}
