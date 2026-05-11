# Unity Toolbox Test Center

`com.unitytoolbox.test-center` is a reusable editor-only Unity package for running categorized `EditMode` and `PlayMode` test suites from a single custom window.

## Features

- categorized suite buttons backed by Unity Test Framework filters
- sequential queue execution for multiple suites
- XML result persistence through `TestRunnerApi.SaveResultToFile`
- latest `EditMode` and `PlayMode` log discovery
- window state persistence through `SessionState`
- suite search by display name, category, mode, result file, or description

## Package Contents

- `Editor/UnityToolbox.TestCenter.Editor.asmdef`
- `Editor/ReusableTestCenterModels.cs`
- `Editor/ReusableTestCenterRunner.cs`
- `Editor/ReusableTestCenterWindowBase.cs`

## Dependency

The package requires `com.unity.test-framework`.

## Integration

Create two thin project-side wrappers:

1. a static catalog that defines suites, sections, and a single `ReusableTestCenterRunner`
2. an `EditorWindow` inheriting from `ReusableTestCenterWindowBase`

Minimal example:

```csharp
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
```

## Suite Contract

Each `ReusableTestSuiteDefinition` maps directly to a Unity Test Runner `Filter`:

- `DisplayName`: visible button label
- `CategoryName`: NUnit/UTF category used in `Filter.categoryNames`
- `Mode`: `EditMode` or `PlayMode`
- `ResultFileName`: XML result file name inside the configured results directory

Your tests must be tagged with matching categories.

```csharp
[Category("MyProjectEditSuite")]
public sealed class SomeEditModeTests
{
}
```

## Results

Default output folder:

- `Temp/TestResults`

Override it through the `resultsDirectoryRelativePath` constructor argument of `ReusableTestCenterDefinition`.

The runner writes:

- `*.xml` result files
- `EditMode.current` and `PlayMode.current` marker files
- timestamped `*.log` files
