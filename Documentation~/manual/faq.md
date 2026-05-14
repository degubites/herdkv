# FAQ

## Where are Unity databases stored?

`HerdKVUnity` stores data under `Application.persistentDataPath/HerdKV`.

## Can I store JSON?

Yes. Encode JSON as UTF-8 bytes or write a custom codec.

## Does delete remove bytes immediately?

No. Delete writes a tombstone. Run `CompactAsync` to rewrite live data into fresh segments.

## Can I use it outside Unity?

Yes. Reference `Runtime/Core/Degubites.HerdKV.Core.csproj` from a .NET project.
