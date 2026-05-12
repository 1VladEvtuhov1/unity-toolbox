# Unity Toolbox Test Center

`com.unitytoolbox.test-center` is a reusable editor-only Unity package for running categorized `EditMode` and `PlayMode` test suites from a single window.

## Features

- categorized suite buttons backed by Unity Test Framework filters
- sequential queue execution for multiple suites
- XML result persistence through `TestRunnerApi.SaveResultToFile`
- latest `EditMode` and `PlayMode` log discovery
- window state persistence through `SessionState`
- suite search by display name, category, mode, result file, or description
- built-in fallback window under `Window/Testing/Unity Toolbox Test Center`
- onboarding flow when no project-specific configuration exists

## Package Contents

- `Editor/UnityToolbox.TestCenter.Editor.asmdef`
- `Editor/ReusableTestCenterConfigurationAsset.cs`
- `Editor/ReusableTestCenterModels.cs`
- `Editor/ReusableTestCenterRunner.cs`
- `Editor/UnityToolboxTestCenterWindow.cs`
- `Editor/ReusableTestCenterWindowBase.cs`

## Dependency

The package requires `com.unity.test-framework`.

## Default Window

The package now exposes a built-in menu entry:

- `Window/Testing/Unity Toolbox Test Center`

If the project has no configuration yet, the window still opens and shows onboarding UI instead of failing.

Runner initialization is deferred until the window binds to it, so window creation itself does not touch `SessionState`.

To use the built-in window without writing any wrapper code:

1. create a `ReusableTestCenterConfigurationAsset`
2. define one or more sections with suite entries
3. tag tests with the matching category names

You can create the asset from either location:

- the onboarding button inside the window
- `Assets/Create/Unity Toolbox/Test Center Configuration`

## Project Customization

If a project needs custom sections, alternative menus, or additional UI, keep using a project-side wrapper. The reusable API remains unchanged.

Typical project-side setup:

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

## Configuration Asset Contract

Each suite entry in `ReusableTestCenterConfigurationAsset` maps directly to a Unity Test Runner `Filter`:

- `Display Name`: visible button label
- `Category Name`: NUnit/UTF category used in `Filter.categoryNames`
- `Mode`: `EditMode` or `PlayMode`
- `Result File Name`: XML result file name inside the configured results directory
- `Description`: optional helper text shown in the window

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

For the built-in window, the same value is configured on `ReusableTestCenterConfigurationAsset`.

The runner writes:

- `*.xml` result files
- `EditMode.current` and `PlayMode.current` marker files
- timestamped `*.log` files

## Package Import Fallback

If Unity 6 keeps importing editor assets from a git package unreliably in your environment, use one of these supported fallbacks:

- embed the package into `Packages/`
- add it as a local package from disk

This is the recommended operational fallback for editor-only tooling. It keeps the package usable even when a specific Unity version handles git package asset import inconsistently.
