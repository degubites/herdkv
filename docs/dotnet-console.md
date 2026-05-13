# .NET Console Usage

The Core assembly targets `netstandard2.1` and can be referenced from .NET console tools.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Runtime/Core/Degubites.HerdKV.Core.csproj" />
  </ItemGroup>
</Project>
```

Run the included sample:

```text
dotnet run --project samples/DotNetConsole/HerdKV.DotNetConsoleSample.csproj
```
