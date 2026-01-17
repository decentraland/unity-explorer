# Debug DCL - Orchestrator

You are debugging an exception in the decentraland/unity-explorer project. You will orchestrate multiple parallel investigations to find the root cause.

## Project Context

- Unity 6.3 with Arch ECS (not Unity DOTS)
- Two worlds: Global World (single instance) and Scene Worlds (per JS scene)
- Plugin system: `IDCLPlugin`, `DCLGlobalPluginBase<TSettings>`, `DCLWorldPluginBase`
- Containers: `StaticContainer`, `DynamicWorldContainer`
- Async: `UniTask`, `AssetPromise<TAsset, TLoadingIntention>`
- Assets: `IAssetsProvisioner`, Addressables, `ProvidedAsset<T>`

## Step 1: Parse Exception

Extract from the stack trace:
```
EXCEPTION_TYPE: [e.g., NullReferenceException]
MESSAGE: [exception message]
CRASH_FILE: [filename]
CRASH_LINE: [line number]
CRASH_METHOD: [method name]
CALL_CHAIN: [full stack]
```

Classify the crash context:
- [ ] ECS System (`*System.cs`, has `Update()`, uses `Query<>`)
- [ ] Plugin (`*Plugin.cs`, implements `IDCLPlugin`)
- [ ] Controller (`*Controller.cs`, MVC pattern)
- [ ] Async/Promise (inside `UniTask`, callback, or `LoadSystemBase`)
- [ ] MonoBehaviour (Unity lifecycle)

## Step 2: Read Crash Site

```bash
cat -n [CRASH_FILE]
```

Identify ALL expressions on the crash line that could be null. List them:
```
NULL_CANDIDATES:
1. [expression] - [why it might be null]
2. [expression] - [why it might be null]
```

## Step 3: Spawn Parallel Investigations

Based on the crash context and null candidates, spawn these sub-agents using TodoWrite. **Spawn ALL relevant investigations in parallel:**

### If ECS-related (components, entities, queries):
```
Todo: [ECS Investigation] Trace component lifecycle for [COMPONENT_NAME]
- Find where component is defined (struct/class)
- Find all systems that Add/Remove this component
- Find all systems that Query for this component
- Check system execution order ([UpdateInGroup], [UpdateAfter], [UpdateBefore])
- Determine if component could be missing at crash point
Files to search: Explorer/Assets/DCL/ECS/, Explorer/Assets/Scripts/
```

### If Plugin/DI-related (services, settings, injection):
```
Todo: [Plugin Investigation] Trace initialization of [SERVICE/PLUGIN_NAME]
- Find plugin registration and settings class
- Check PluginSettingsContainer configuration
- Trace dependency injection through containers
- Check initialization order and world injection timing
- Verify all AssetReferenceT<> and ComponentReference<> are assigned
Files to search: Explorer/Assets/DCL/Plugins/, *Container.cs, *Settings.cs
```

### If Asset-related (loading, addressables, promises):
```
Todo: [Asset Investigation] Trace asset loading for [ASSET_EXPRESSION]
- Find IAssetsProvisioner usage
- Trace ProvidedAsset/ProvidedInstance lifecycle
- Check if asset reference is configured in settings
- Verify async loading completion before .Value access
- Check for disposal/cleanup issues
Files to search: *Provisioner*, *Asset*, *Settings.cs
```

### If Async-related (UniTask, cancellation, promises):
```
Todo: [Async Investigation] Trace async flow for [METHOD_NAME]
- Find all await points and UniTask usage
- Check CancellationToken handling
- Trace AssetPromise resolution
- Check for Result usage after cancellation
- Verify SuppressToResultAsync patterns
Files to search: *.cs files with async, UniTask, CancellationToken
```

### If World/Entity lifecycle-related:
```
Todo: [Lifecycle Investigation] Trace entity/world lifecycle for [ENTITY/WORLD]
- Check EntityReference vs raw Entity usage
- Find world injection points (ArchSystemsWorldBuilder)
- Trace SingleInstanceEntity caching
- Check disposal and cleanup order
- Verify Global vs Scene world context
Files to search: *World*, *Entity*, *System.cs
```

### Always spawn - Adjacent Code Review:
```
Todo: [Adjacent Code] Review related code in [CRASH_FILE_DIRECTORY]
- Check other systems in same SystemGroup
- Look for similar patterns that might fail
- Find recent changes (git blame if available)
- Check for TODO/FIXME comments about null handling
```

## Step 4: Await Results

After spawning investigations, wait for all sub-agents to complete. Collect their findings:

```
FINDINGS:
- ECS: [summary]
- Plugin: [summary]
- Asset: [summary]
- Async: [summary]
- Lifecycle: [summary]
- Adjacent: [summary]
```

## Step 5: Synthesize

Once all investigations complete, determine:

1. **What is null?** (the immediate cause)
2. **Where does the null originate?** (trace back through DCL systems)
3. **Why is it null?** (the root cause in DCL architecture terms)
4. **When does this occur?** (conditions, timing, order of operations)

## Step 6: Generate Report

```markdown
# DCL Exception Analysis

## Summary
| | |
|---|---|
| **Exception** | [TYPE]: [MESSAGE] |
| **Location** | [FILE:LINE] |
| **Context** | [ECS System / Plugin / Controller / Async] |
| **Root Cause** | [One sentence] |
| **Confidence** | [High / Medium / Low] |

## Investigation Results

### ECS Analysis
[Findings from ECS sub-agent]

### Plugin/DI Analysis
[Findings from Plugin sub-agent]

### Asset Loading Analysis
[Findings from Asset sub-agent]

### Async Flow Analysis
[Findings from Async sub-agent]

### Lifecycle Analysis
[Findings from Lifecycle sub-agent]

## Root Cause

**Category**: [ECS Component Missing / Asset Not Loaded / Plugin Not Initialized / World Not Injected / Async Cancelled / Entity Destroyed]

[Detailed explanation referencing specific findings]

## Causal Chain

```
1. [Root cause - e.g., "Component not added in InitializeSystem"]
   ↓
2. [Propagation - e.g., "RenderSystem queries for component"]
   ↓
3. [Failure - e.g., "entity.Get<T>() throws because component missing"]
```

## Recommended Fixes

### Immediate Fix (stop the crash)
**File**: [file:line]
```csharp
// Add defensive check
```

### Root Cause Fix (prevent the null)
**File**: [file:line]
```csharp
// Fix initialization/registration/lifecycle
```

### Architectural Improvements
- [ ] [Suggestion 1]
- [ ] [Suggestion 2]

## Verification Checklist
- [ ] Check PluginSettingsContainer in Unity Inspector
- [ ] Verify system execution order
- [ ] Confirm asset references assigned
- [ ] Test with fresh scene load
- [ ] Check for race conditions in async code

## Files Examined
| File | Investigation | Finding |
|------|---------------|---------|
| [file1] | [which sub-agent] | [key finding] |
```

---

$ARGUMENTS
