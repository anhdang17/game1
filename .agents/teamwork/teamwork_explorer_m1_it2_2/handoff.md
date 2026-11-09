# Handoff Report: MSBuild Configuration Remediation for Root Project Globbing (BUILD-01)

**Agent**: `teamwork_explorer_m1_it2_2`  
**Roles**: explorer, investigator, synthesizer  
**Target File**: `/Users/mac/Projects/game1/GameProject.csproj`  
**Related File**: `/Users/mac/Projects/game1/Tests/GameProject.Tests.csproj`  
**Date**: 2026-11-08  
**State Folder**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_2/`  

---

## 1. Observation

### 1.1 Direct Reproduction of Gate Failure (BUILD-01)
1. Running `dotnet build GameProject.csproj` from `/Users/mac/Projects/game1`:
   - Command: `dotnet build GameProject.csproj`
   - Exit code: 1
   - Errors: 444 errors, 5 warnings.
   - Error categories:
     - Duplicate assembly attributes:
       - `obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs(4,12): error CS0579: Duplicate 'global::System.Runtime.Versioning.TargetFrameworkAttribute' attribute`
       - `obj/Debug/net10.0/GameProject.AssemblyInfo.cs(13,12): error CS0579: Duplicate 'System.Reflection.AssemblyCompanyAttribute' attribute`
     - Missing xUnit symbols:
       - `Tests/AdversarialCoreEngineStressTests.cs(532,6): error CS0246: The type or namespace name 'InlineData' could not be found`
       - `Tests/AdversarialCoreEngineStressTests.cs(546,6): error CS0246: The type or namespace name 'Fact' could not be found`
       - `Tests/QuestFlowTests.cs(41,6): error CS0246: The type or namespace name 'FactAttribute' could not be found`
2. Running `dotnet build` directly from root directory `/Users/mac/Projects/game1`:
   - Command: `dotnet build`
   - Exit code: 1
   - Result: Identical 444 compiler errors because MSBuild resolves the root project `GameProject.csproj`.

### 1.2 SDK Globbing Inspection via MSBuild Engine
1. Inspection of default property values:
   - Command: `dotnet msbuild GameProject.csproj -getProperty:DefaultItemExcludes`
   - Value: `;bin/Debug//**;obj/Debug//**;bin//**;obj//**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store`
   - Analysis: Note that `bin//**` and `obj//**` only exclude folders directly at the project root (`/Users/mac/Projects/game1/bin/` and `/Users/mac/Projects/game1/obj/`). Nested directories such as `Tests/bin/` and `Tests/obj/` are NOT covered by the default exclusion pattern.
2. Item globbing without exclusions:
   - Command: `dotnet msbuild GameProject.csproj -getItem:Compile`
   - Result: Output includes all test files (`Tests/QuestFlowTests.cs`, `Tests/SaveLoadTests.cs`, etc.) and intermediate files (`Tests/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`).
   - Command: `dotnet msbuild GameProject.csproj -getItem:None`
   - Result: Output includes test binaries from `Tests/bin/Debug/net10.0/...`.
3. In `Microsoft.NET.Sdk.DefaultItems.props` (lines 36-43):
   - `<Compile Include="**/*$(DefaultLanguageSourceExtension)" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" Condition=" '$(EnableDefaultCompileItems)' == 'true' " />`
   - `<None Include="**/*" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />`
   - All default SDK items respect `$(DefaultItemExcludes)`.

### 1.3 Validation with Exclusion Applied
1. Command: `dotnet build GameProject.csproj -p:DefaultItemExcludes="Tests/**"`
   - Output:
     ```
     GameProject -> /Users/mac/Projects/game1/bin/Debug/net10.0/GameProject.dll
     Build succeeded.
         0 Warning(s)
         0 Error(s)
     ```
2. Command: `dotnet msbuild GameProject.csproj -getItem:Compile -p:DefaultItemExcludes="Tests/**"`
   - Result: Contains ONLY `Program.cs` and `Source/Core/**/*.cs`. 0 files from `Tests/`.
3. Command: `dotnet msbuild GameProject.csproj -getItem:None -p:DefaultItemExcludes="Tests/**" | grep -i "Tests"`
   - Result: 0 matches (exit code 1).
4. Command: `dotnet run --project GameProject.csproj -p:DefaultItemExcludes="Tests/**"`
   - Output:
     ```
     Sound loaded, frameCount: 12000
     Audio test passed!
     ```
5. Test suite execution:
   - Command: `dotnet test Tests/GameProject.Tests.csproj`
   - Output: `Passed! - Failed: 0, Passed: 125, Skipped: 0, Total: 125, Duration: 80 ms`.

---

## 2. Logic Chain

1. *From Observation 1.1*:
   Because `GameProject.csproj` is at the workspace root and references the standard .NET SDK (`<Project Sdk="Microsoft.NET.Sdk">`), the default SDK wildcard `Compile Include="**/*.cs"` recursively globs all `.cs` files in subdirectories, including all test files in `Tests/` and intermediate generated files in `Tests/obj/`.
2. *From Observation 1.1 (Errors)*:
   Because `GameProject.csproj` is an application project without references to xUnit (`xunit`, `Microsoft.NET.Test.Sdk`), compiling `Tests/**/*.cs` into `GameProject.dll` fails with 444 missing attribute/namespace errors (`[Fact]`, `[InlineData]`, `Assert`). Additionally, `Tests/obj/...AssemblyAttributes.cs` conflicts with root `obj/...AssemblyAttributes.cs`, throwing duplicate attribute errors `CS0579`.
3. *From Observation 1.2*:
   MSBuild evaluates properties in Pass 1 and items in Pass 2. By defining:
   ```xml
   <PropertyGroup>
     <DefaultItemExcludes>$(DefaultItemExcludes);Tests/**</DefaultItemExcludes>
   </PropertyGroup>
   ```
   the SDK's item globbing pattern `<Compile Include="**/*$(DefaultLanguageSourceExtension)" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />` receives `Tests/**` in its exclude list during Pass 2. This prevents `Tests/**/*.cs`, `Tests/obj/**/*.cs`, and `Tests/bin/**/*` from ever entering `@(Compile)`, `@(None)`, or `@(Content)`.
4. *From Item Collection Defense-in-Depth*:
   Adding:
   ```xml
   <ItemGroup>
     <Compile Remove="Tests/**" />
   </ItemGroup>
   ```
   guarantees that even if an explicit include or external targets file injects items from `Tests/**`, they are purged from the compilation set.
5. *From Observation 1.3*:
   Excluding `Tests/**` allows `GameProject.csproj` to build cleanly (0 warnings, 0 errors) and execute without interfering with `Tests/GameProject.Tests.csproj`.
6. *From ProjectReference Analysis*:
   Currently, `/Users/mac/Projects/game1/Tests/GameProject.Tests.csproj` line 22 uses a workaround:
   `<ProjectReference Include="..\GameProject.csproj" AdditionalProperties="DefaultItemExcludes=Tests/**" Aliases="global,CoreApp" />`.
   Once `GameProject.csproj` natively excludes `Tests/**`, `AdditionalProperties="DefaultItemExcludes=Tests/**"` can either remain (non-breaking) or be cleanly removed so that MSBuild shares the exact same build cache instance for `GameProject.csproj`.

---

## 3. Caveats

1. **Scope Boundary**:
   This investigation addresses root project configuration and globbing exclusions (BUILD-01). It does not alter or delete files in `Tests/Contracts/` (Finding 1 from reviewer handoff, which is planned for post-Milestone 4 cleanup).
2. **Path Delimiters**:
   MSBuild automatically normalizes forward slash (`/`) and backslash (`\`) across Windows, macOS, and Linux. `Tests/**` functions identically on all supported platforms.
3. **No Direct Code Modifications Performed**:
   In strict adherence to Explorer read-only role constraints, no source code or project files were modified. The exact XML edits are specified in Section 4 for the Worker agent.

---

## 4. Conclusion & Worker Implementation Plan

### 4.1 Primary Modification: `GameProject.csproj`
Apply the following changes to `/Users/mac/Projects/game1/GameProject.csproj`:

#### Diff Patch:
```diff
--- a/GameProject.csproj
+++ b/GameProject.csproj
@@ -7,6 +7,11 @@
     <Nullable>enable</Nullable>
     <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
+    <DefaultItemExcludes>$(DefaultItemExcludes);Tests/**</DefaultItemExcludes>
   </PropertyGroup>
 
+  <ItemGroup>
+    <Compile Remove="Tests/**" />
+  </ItemGroup>
+
   <ItemGroup>
     <PackageReference Include="Raylib-cs" Version="8.1.0" />
   </ItemGroup>
```

#### Full Target Content (`GameProject.csproj`):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <DefaultItemExcludes>$(DefaultItemExcludes);Tests/**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="Tests/**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Raylib-cs" Version="8.1.0" />
  </ItemGroup>

</Project>
```

### 4.2 Secondary / Cleanup Modification (Optional but Recommended): `Tests/GameProject.Tests.csproj`
In `/Users/mac/Projects/game1/Tests/GameProject.Tests.csproj`, line 22 can be simplified from:
```xml
    <ProjectReference Include="..\GameProject.csproj" AdditionalProperties="DefaultItemExcludes=Tests/**" Aliases="global,CoreApp" />
```
to:
```xml
    <ProjectReference Include="..\GameProject.csproj" Aliases="global,CoreApp" />
```
This removes the redundant workaround and ensures both root build and test project share the exact same MSBuild configuration cache.

---

## 5. Verification Method

Once the Worker applies the patch, verify with the following commands:

### 5.1 Root Project Standalone Build Verification
```bash
cd /Users/mac/Projects/game1
dotnet build GameProject.csproj
```
*Expected Result*:
`Build succeeded. 0 Warning(s), 0 Error(s)` in `GameProject.dll`.

### 5.2 Root Directory Default Build Verification
```bash
cd /Users/mac/Projects/game1
dotnet build
```
*Expected Result*:
`Build succeeded. 0 Warning(s), 0 Error(s)`.

### 5.3 Compile Item Inspection Check
```bash
dotnet msbuild /Users/mac/Projects/game1/GameProject.csproj -getItem:Compile | grep -i "Tests"
```
*Expected Result*:
0 output lines (exit code 1).

### 5.4 Automated Test Suite Verification
```bash
dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
```
*Expected Result*:
All 125 tests pass in < 150 ms (`Passed: 125, Failed: 0`).
