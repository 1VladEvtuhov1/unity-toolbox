# Unity Toolbox

`unity-toolbox` is a mono-repo with reusable Unity UPM packages.

## Packages

- `com.unitytoolbox.test-center`
  Reusable editor test center for running categorized `EditMode` and `PlayMode` suites through the Unity Test Framework.

## Installation

Add the required package to another Unity project through `Packages/manifest.json`.

Example:

```json
{
  "dependencies": {
    "com.unitytoolbox.test-center": "https://github.com/1VladEvtuhov1/unity-toolbox.git?path=/Packages/com.unitytoolbox.test-center"
  }
}
```

For stable consumption, pin a tag instead of tracking a moving branch:

```json
{
  "dependencies": {
    "com.unitytoolbox.test-center": "https://github.com/1VladEvtuhov1/unity-toolbox.git?path=/Packages/com.unitytoolbox.test-center#v1.0.0"
  }
}
```
