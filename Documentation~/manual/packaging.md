# Packaging

HerdKV is a Unity Package Manager package whose repository root is the package root.

The package contains:

- `Runtime/Core` for the pure C# storage engine
- `Runtime/Unity` for Unity helpers
- `Editor` for the database viewer
- `Samples~` for importable Unity samples
- `Documentation~` for package documentation
- `Development~` for tests, benchmarks, and .NET console samples that Unity should ignore

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
