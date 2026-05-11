using System;
using UnityEditor.TestTools.TestRunner.Api;

namespace UnityToolbox.TestCenter
{
    public readonly struct ReusableTestCenterDefinition
    {
        public ReusableTestCenterDefinition(
            string windowTitle,
            string toolbarTitle,
            string sessionStatePrefix,
            string resultsDirectoryRelativePath = "Temp/TestResults",
            float minWidth = 780f,
            float minHeight = 540f)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                throw new ArgumentException("Window title must be assigned.", nameof(windowTitle));

            if (string.IsNullOrWhiteSpace(sessionStatePrefix))
                throw new ArgumentException("Session state prefix must be assigned.", nameof(sessionStatePrefix));

            if (minWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minWidth), minWidth, "Minimum width must be greater than zero.");

            if (minHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minHeight), minHeight, "Minimum height must be greater than zero.");

            WindowTitle = windowTitle;
            ToolbarTitle = string.IsNullOrWhiteSpace(toolbarTitle) ? windowTitle : toolbarTitle;
            SessionStatePrefix = sessionStatePrefix;
            ResultsDirectoryRelativePath = string.IsNullOrWhiteSpace(resultsDirectoryRelativePath)
                ? "Temp/TestResults"
                : resultsDirectoryRelativePath;
            MinWidth = minWidth;
            MinHeight = minHeight;
        }

        public string WindowTitle { get; }

        public string ToolbarTitle { get; }

        public string SessionStatePrefix { get; }

        public string ResultsDirectoryRelativePath { get; }

        public float MinWidth { get; }

        public float MinHeight { get; }
    }

    public readonly struct ReusableTestSuiteDefinition
    {
        public ReusableTestSuiteDefinition(string displayName, string categoryName, TestMode mode, string resultFileName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name must be assigned.", nameof(displayName));

            if (string.IsNullOrWhiteSpace(categoryName))
                throw new ArgumentException("Category name must be assigned.", nameof(categoryName));

            if (string.IsNullOrWhiteSpace(resultFileName))
                throw new ArgumentException("Result file name must be assigned.", nameof(resultFileName));

            DisplayName = displayName;
            CategoryName = categoryName;
            Mode = mode;
            ResultFileName = resultFileName;
        }

        public string DisplayName { get; }

        public string CategoryName { get; }

        public TestMode Mode { get; }

        public string ResultFileName { get; }
    }

    public readonly struct ReusableTestSuiteEntry
    {
        public ReusableTestSuiteEntry(ReusableTestSuiteDefinition suite, string description)
        {
            Suite = suite;
            Description = description ?? string.Empty;
        }

        public ReusableTestSuiteDefinition Suite { get; }

        public string Description { get; }
    }

    public readonly struct ReusableTestSuiteSection
    {
        public ReusableTestSuiteSection(string title, ReusableTestSuiteEntry[] entries)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Section title must be assigned.", nameof(title));

            Title = title;
            Entries = entries ?? Array.Empty<ReusableTestSuiteEntry>();
        }

        public string Title { get; }

        public ReusableTestSuiteEntry[] Entries { get; }
    }
}
