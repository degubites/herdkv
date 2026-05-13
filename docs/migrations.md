# Migrations

HerdKV stores schema version under `__herdkv/schema-version`.

```csharp
HerdKVOpenOptions options = new() { SchemaVersion = 2 };
options.Migrations.Add(new V0ToV1Migration());
options.Migrations.Add(new V1ToV2Migration());

await using IHerdKVStore db = await HerdKVStore.OpenAsync("./save", options);
```

Migration rules:

- migrations are forward-only
- missing migration steps fail before data changes
- opening with a lower target version fails
- migration failure restores a backup by default
