# Packaging

HerdKV is a Unity Package Manager package whose repository root is the package root.

The package contains:

- `Runtime/Core` for the pure C# storage engine
- `Runtime/Unity` for Unity helpers
- `Editor` for the database viewer
- `Samples~` for importable Unity samples
- `docs` and `Documentation~` for package documentation
- `tests`, `benchmarks`, and `samples/DotNetConsole` for package development outside Unity

UPM git URL:

```text
https://codeberg.org/degubites/herdkv.git
```

Release checklist:

- run Core tests
- run the console sample
- run benchmarks when performance changes
- open the package in Unity
- import samples
- smoke test the Editor Viewer
