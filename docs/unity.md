# Unity Helpers

`HerdKVUnity` resolves database paths under `Application.persistentDataPath/HerdKV`.

```csharp
await using IHerdKVStore db = await HerdKVUnity.OpenAsync("main-save");
```

Save slots:

```csharp
await using IHerdKVStore slot = await HerdKVUnity.OpenSlotAsync("slot-1");
```

Use `HerdKVSettings` when you want a reusable ScriptableObject for database name, segment size, checksum validation, and schema version.
