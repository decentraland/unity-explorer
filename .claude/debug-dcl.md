# Debug Stack Trace - Decentraland Unity Explorer

You are debugging an exception in the decentraland/unity-explorer project. This is a Unity 6 project using the Arch ECS framework, UniTask for async operations, and a plugin-based architecture.

## Project Context

**Architecture:**
- ECS-based using Arch library (not Unity DOTS)
- Two world types: Global World (single instance) and Scene Worlds (per JavaScript scene)
- Plugin system: `IDCLPlugin` with `DCLGlobalPluginBase<TSettings>` and world plugins
- Dependency injection via containers: `StaticContainer`, `DynamicWorldContainer`
- Asset loading via `IAssetsProvisioner` and Addressables
- Async operations use `UniTask` with `AssetPromise<TAsset, TLoadingIntention>` pattern

**Key Directories:**
```
Explorer/
├── Assets/
│   ├── DCL/                    # Core Decentraland logic
│   │   ├── ECS/               # Entity-Component-System
│   │   │   ├── Components/    # Data structures
│   │   │   └── Systems/       # Logic that transforms components
│   │   ├── Plugins/           # Feature plugins
│   │   └── Controllers/       # MVC controllers
│   ├── Scripts/
│   │   ├── Global/            # Global world systems
│   │   └── SceneRuntime/      # Per-scene systems
│   └── Protocol/              # Protobuf definitions
```

**Common Null Sources in This Project:**
1. **ECS Queries**: Entity doesn't have expected component
2. **Asset Promises**: `ProvidedAsset<T>` or `ProvidedInstance<T>` not resolved
3. **Plugin Settings**: `IDCLPluginSettings` fields not configured in `PluginSettingsContainer`
4. **Addressable References**: `AssetReferenceT<T>` or `ComponentReference<T>` not loaded
5. **World/Entity Access**: Accessing disposed world or destroyed entity
6. **SingleInstanceEntity**: Not cached or world not injected yet
7. **Scene Facade**: `ISceneFacade` accessed before scene initialization
8. **UniTask Cancellation**: Operation cancelled but result used

## Investigation Protocol

### Step 1: Parse the Stack Trace

Extract:
- Exception type and message
- Crash file, line, method
- Full call chain
- Identify if it's in a System, Plugin, Controller, or async callback

### Step 2: Read Crash Site

```bash
# Read the failing file
cat -n [FILE]
```

Identify null candidates specific to this project:
- `entity.Get<TComponent>()` calls
- `world.Query<T>()` results
- `.Value` access on promises/nullable
- `_settings.SomeReference` access
- `SingleInstanceEntity` access

### Step 3: Project-Specific Investigations

**For ECS-related nulls:**
```bash
# Find component registration
rg "struct.*Component|class.*Component" -n Explorer/Assets --type cs

# Find systems that modify this component
rg "Query<.*ComponentName" -n Explorer/Assets --type cs

# Check if component is added
rg "\.Add\(.*ComponentName|AddComponent.*ComponentName" -n Explorer/Assets --type cs
```

**For Plugin/DI nulls:**
```bash
# Find plugin registration
rg "class.*Plugin.*:.*IDCLPlugin|DCLGlobalPluginBase|DCLWorldPluginBase" -n Explorer/Assets --type cs

# Find settings configuration
rg "IDCLPluginSettings|PluginSettingsContainer" -n Explorer/Assets --type cs

# Check container initialization
rg "StaticContainer|DynamicWorldContainer" -n Explorer/Assets --type cs
```

**For Asset loading nulls:**
```bash
# Find asset provisioning
rg "IAssetsProvisioner|ProvideMainAssetAsync|ProvidedAsset|ProvidedInstance" -n Explorer/Assets --type cs

# Check addressable references
rg "AssetReferenceT|ComponentReference" -n Explorer/Assets --type cs
```

**For UniTask/async nulls:**
```bash
# Find async patterns
rg "UniTask|async.*Task|\.AsUniTask|SuppressToResultAsync" -n Explorer/Assets --type cs

# Check cancellation handling
rg "CancellationToken|\.IsCancellationRequested|ThrowIfCancellationRequested" -n Explorer/Assets --type cs

# Find promise patterns
rg "AssetPromise|LoadSystemBase" -n Explorer/Assets --type cs
```

**For World/Entity lifecycle nulls:**
```bash
# Check world injection
rg "ArchSystemsWorldBuilder|InjectToWorld|GlobalPluginArguments" -n Explorer/Assets --type cs

# Find entity references
rg "EntityReference|SingleInstanceEntity" -n Explorer/Assets --type cs

# Check disposal patterns
rg "IDisposable|Dispose\(\)|\.Destroy\(" -n Explorer/Assets --type cs
```

### Step 4: Check Related Systems

```bash
# Find systems in same group
rg "\[UpdateInGroup\(typeof\(.*Group" -n [CRASH_FILE_DIR] --type cs

# Find system dependencies
rg "\[UpdateAfter\(typeof|UpdateBefore\(typeof" -n Explorer/Assets --type cs
```

### Step 5: Check Scene vs Global World Context

Determine if the crash is in:
- Global world (single instance, handles realms, player, camera, avatars)
- Scene world (per-scene, JavaScript scene reflection)

```bash
# Check world type
rg "GlobalWorld|SceneWorld|ISceneFacade" -n [CRASH_FILE] --type cs
```

## Common Patterns in This Project

### Pattern: Unresolved Asset Promise
```csharp
// Problem: Accessing .Value before promise resolved
var asset = await assetsProvisioner.ProvideMainAssetAsync(settings.SomeRef, ct);
var component = asset.Value.GetComponent<T>(); // Null if not resolved

// Check: Is the AssetReference configured in PluginSettingsContainer?
```

### Pattern: ECS Component Not Added
```csharp
// Problem: Querying entity without component
ref var component = ref entity.Get<SomeComponent>(); // Throws if missing

// Check: Is component added in initialization system?
```

### Pattern: World Not Injected Yet
```csharp
// Problem: Using world before ContinueInitialization called
protected override async UniTask<ContinueInitialization?> InitializeAsyncInternal(...)
{
    // Assets loaded here...
    return (ref ArchSystemsWorldBuilder<World> world, in GlobalPluginArguments _) =>
    {
        // World available here only
    };
}
```

### Pattern: Disposed/Destroyed Access
```csharp
// Problem: Using entity after scene disposed
// Check: Is there proper cleanup in Dispose()? Is entity reference stale?
```

### Pattern: Cancelled UniTask
```csharp
// Problem: Using result after cancellation
// Check: Is CancellationToken checked before using results?
if (ct.IsCancellationRequested)
    return Result.CancelledResult();
```

## Output Format

```markdown
# Stack Trace Analysis: [BRIEF_DESCRIPTION]

## Summary
| | |
|---|---|
| **Exception** | [Type]: [Message] |
| **Location** | [File:Line] in [Method] |
| **Context** | [Global Plugin / Scene System / Controller / Async callback] |
| **Null Value** | [What was null] |

## Root Cause

**Category**: [ECS Component / Asset Loading / Plugin Init / World Lifecycle / Async/Cancel]

[Explanation of why the value is null in context of DCL architecture]

## Evidence

### Crash Site
```csharp
[Code snippet]
```

### Origin of Null
```csharp
[Code showing where null comes from]
```

## Causal Chain
1. [Initial condition]
2. [How it propagates through DCL systems]
3. [Null dereference]

## Fixes

### Immediate Fix
```csharp
// Add null check or TryGet pattern
if (entity.TryGet<Component>(out var comp))
{
    // safe to use
}
```

### Root Cause Fix
```csharp
// Fix initialization, registration, or lifecycle
```

### DCL-Specific Recommendations
- [ ] Check PluginSettingsContainer in Unity Inspector
- [ ] Verify system update order with [UpdateAfter/Before]
- [ ] Ensure component is added before querying
- [ ] Check asset Addressable is assigned
- [ ] Verify world injection timing

## Files to Review
| File | Relevance |
|------|-----------|
| [file] | [why] |
```

## Execute Now

1. Parse the exception
2. Identify if it's ECS, Plugin, Asset, or Async related
3. Use project-specific search patterns
4. Check component lifecycle and world context
5. Provide DCL-architecture-aware fix

---

$ARGUMENTS