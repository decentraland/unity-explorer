# Investigate Asset Loading - Sub-agent

You are investigating an asset loading related null in decentraland/unity-explorer.

## Parameters
- ASSET_EXPRESSION: The asset access that might be null
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number

## DCL Asset Loading Context

- **IAssetsProvisioner**: Main interface for loading assets
- **ProvidedAsset<T>**: Wrapper for loaded ScriptableObjects, Materials, etc.
- **ProvidedInstance<T>**: Wrapper for instantiated prefabs (ComponentReference)
- **AssetReferenceT<T>**: Addressable reference to assets
- **ComponentReference<T>**: Reference to component on prefab
- **AssetPromise<TAsset, TLoadingIntention>**: ECS-friendly async loading pattern

## Investigation Tasks

### 1. Find Asset Access at Crash Site
```bash
sed -n '{{CRASH_LINE-10}},{{CRASH_LINE+10}}p' {{CRASH_FILE}} | cat -n
```
- What asset is being accessed?
- Is `.Value` used directly?
- Is there null checking?

### 2. Find Asset Reference Definition
```bash
rg "AssetReferenceT|AssetReference<|ComponentReference" -n Explorer/Assets --type cs | grep -i "$(echo {{ASSET_EXPRESSION}} | grep -oE '[A-Za-z]+' | head -1)"
```
- Where is the reference defined?
- What type is it?

### 3. Find Asset Loading Call
```bash
rg "ProvideMainAssetAsync|ProvideInstanceAsync|assetsProvisioner\." -n {{CRASH_FILE}} --type cs -B 3 -A 5
```
- Where is the asset loaded?
- Is loading awaited properly?
- Is CancellationToken handled?

### 4. Check Loading Pattern
```bash
rg "ProvidedAsset|ProvidedInstance|\.Value" -n {{CRASH_FILE}} --type cs -B 2 -A 2
```

**Safe pattern:**
```csharp
var provided = await assetsProvisioner.ProvideMainAssetAsync(settings.AssetRef, ct: ct);
if (provided.Value != null) // Check before use
    DoSomething(provided.Value);
```

**Dangerous pattern:**
```csharp
var provided = await assetsProvisioner.ProvideMainAssetAsync(settings.AssetRef, ct: ct);
DoSomething(provided.Value); // Crashes if not resolved
```

### 5. Check Settings Configuration
```bash
rg "Settings" -n {{CRASH_FILE}} --type cs | head -5
SETTINGS_CLASS=$(rg "class.*Settings" -n {{CRASH_FILE_DIR}} --type cs -l | head -1)
cat -n $SETTINGS_CLASS 2>/dev/null | head -50
```
- What settings class contains this reference?
- Is it IDCLPluginSettings?

### 6. Check AssetPromise Pattern (ECS)
```bash
rg "AssetPromise|LoadSystemBase|TLoadingIntention" -n Explorer/Assets --type cs | grep -i "$(echo {{ASSET_EXPRESSION}} | grep -oE '[A-Za-z]+' | head -1)"
```
- Is this using the ECS promise pattern?
- Is the promise polled before consumption?

### 7. Check Disposal/Cleanup
```bash
rg "Dispose|Release|Unload" -n {{CRASH_FILE}} --type cs
rg "ProvidedAsset.*Dispose|ProvidedInstance.*Dispose" -n Explorer/Assets --type cs
```
- Is asset disposed before access?
- Is there reference counting issue?

### 8. Check Addressables Configuration
```bash
rg "Addressables|AddressableAssetSettings" -n Explorer/Assets --type cs | head -10
```
- Could asset be missing from Addressables?

## Output Format

```markdown
## Asset Investigation: {{ASSET_EXPRESSION}}

### Asset Access
- **Location**: {{CRASH_FILE}}:{{CRASH_LINE}}
- **Expression**: `{{ASSET_EXPRESSION}}`
- **Access Pattern**: [.Value direct / null-checked / TryGet]

### Asset Reference
- **Type**: [AssetReferenceT<X> / ComponentReference<X>]
- **Defined In**: [settings class file:line]
- **Configured**: [Check Unity Inspector]

### Loading Flow
```
1. [Settings.AssetRef defined]
   ↓
2. [ProvideMainAssetAsync called at file:line]
   ↓
3. [await completes - or does it?]
   ↓
4. [.Value accessed at crash site]
```

### Potential Null Scenarios
| Scenario | Likelihood | Evidence |
|----------|------------|----------|
| Reference not assigned in Inspector | [High/Med/Low] | [check required] |
| Loading cancelled | [High/Med/Low] | [CT handling] |
| Loading failed silently | [High/Med/Low] | [error handling] |
| Accessed before await | [High/Med/Low] | [code pattern] |
| Asset disposed | [High/Med/Low] | [disposal code] |

### Issues Found
- [ ] .Value accessed without null check
- [ ] AssetReference not configured in PluginSettingsContainer
- [ ] CancellationToken not checked after await
- [ ] No error handling for load failure
- [ ] Asset disposed before access

### Evidence
```csharp
[Key code snippets showing the issue]
```

### Verification Steps
1. Open PluginSettingsContainer in Unity Inspector
2. Find the settings for [plugin name]
3. Check if [asset reference field] is assigned
4. If missing, assign the correct asset

### Confidence: [High/Medium/Low]
```

## Execute

Trace `{{ASSET_EXPRESSION}}` through DCL's asset loading system and report findings.
