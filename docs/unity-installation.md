# Unity Installation

Install HerdKV as a local UPM package or through a git URL.

## Local Package

1. Keep this package at `Packages/com.degubites.herdkv` in a Unity project.
2. Open Unity Package Manager.
3. Choose `Add package from disk`.
4. Select `Packages/com.degubites.herdkv/package.json`.

## Git URL

```text
https://codeberg.org/degubites/herdkv.git
```

## Basic Unity Usage

```csharp
await using IHerdKVStore db = await HerdKVUnity.OpenAsync("main-save");
await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
```

Databases opened through `HerdKVUnity` are created under `Application.persistentDataPath/HerdKV`.
