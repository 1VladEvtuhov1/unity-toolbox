using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityToolbox.TestCenter;

namespace MyProject.EditorTools.Tests
{
    internal static class MyProjectTestCenter
    {
        internal static readonly ReusableTestCenterDefinition Definition =
            new(
                windowTitle: "Tests",
                toolbarTitle: "My Project Test Center",
                sessionStatePrefix: "MyProject.TestCenter.");

        internal static readonly ReusableTestSuiteDefinition EditModeSuite =
            new("Run EditMode Suite", "MyProjectEditSuite", TestMode.EditMode, "EditModeSuite.xml");

        internal static readonly ReusableTestSuiteDefinition PlayModeSuite =
            new("Run PlayMode Suite", "MyProjectPlaySuite", TestMode.PlayMode, "PlayModeSuite.xml");

        internal static readonly ReusableTestSuiteDefinition[] AllSuites =
        {
            EditModeSuite,
            PlayModeSuite
        };

        internal static readonly ReusableTestSuiteSection[] Sections =
        {
            new(
                "Main Suites",
                new[]
                {
                    new ReusableTestSuiteEntry(EditModeSuite, "Run the full EditMode suite."),
                    new ReusableTestSuiteEntry(PlayModeSuite, "Run the full PlayMode suite.")
                })
        };

        internal static readonly ReusableTestCenterRunner Runner = new(Definition, AllSuites);
    }

    internal sealed class MyProjectTestCenterWindow : ReusableTestCenterWindowBase
    {
        protected override ReusableTestCenterDefinition Definition => MyProjectTestCenter.Definition;

        protected override ReusableTestCenterRunner Runner => MyProjectTestCenter.Runner;

        protected override IReadOnlyList<ReusableTestSuiteSection> Sections => MyProjectTestCenter.Sections;

        [MenuItem("Tests/Open Test Center")]
        private static void Open()
        {
            OpenWindow<MyProjectTestCenterWindow>(MyProjectTestCenter.Definition);
        }
    }
}
