# Investigate Assets - Sub-agent

Investigate asset loading issues in decentraland/unity-explorer.

## Parameters
- ASSET: The asset or reference to investigate
- ISSUE: What's wrong (not loaded, null, disposed, etc.)

## DCL Asset Quick Reference
- `IAssetsProvisioner` loads assets
- `ProvidedAsset<T>` wraps loaded ScriptableObjects, Materials, etc.
- `ProvidedInstance<T>` wraps instantiated prefabs
- `AssetReferenceT<T>` references assets in Addressables
- `ComponentReference<T>` references components on prefabs
- `.Value` access without null check is dangerous
- `AssetPromise<T, TIntent>` for ECS async loading pattern

## Tasks

```bash
# 1. Find asset reference definition
rg "AssetReferenceT|AssetReference<|ComponentReference" -n Explorer/Assets --type cs | grep -i "{{ASSET}}"

# 2. Find where asset is loaded
rg "ProvideMainAssetAsync|ProvideInstanceAsync|assetsProvisioner\." -n Explorer/Assets --type cs | grep -i "{{ASSET}}"

# 3. Find .Value access
rg "\.Value" -n Explorer/Assets --type cs | grep -i "{{ASSET}}"

# 4. Check for null guards
rg "\.Value.*!=.*null|\.Value\s*\?\." -n Explorer/Assets --type cs | grep -i "{{ASSET}}"

# 5. Find settings class containing reference
rg "{{ASSET}}" -n Explorer/Assets --type cs | grep -i "settings"

# 6. Check disposal
rg "Dispose|Release" -n Explorer/Assets --type cs | grep -i "{{ASSET}}"
```

## Output Format

```markdown
## Asset Analysis: {{ASSET}}

### Asset Reference
- **Type**: [AssetReferenceT<X> / ComponentReference<X>]
- **Defined in**: [settings class file:line]
- **Loaded at**: [file:line]
- **Accessed at**: [file:line]

### Loading Pattern
```csharp
// Current pattern
var provided = await assetsProvisioner.ProvideMainAssetAsync(settings.{{ASSET}}, ct);
DoSomething(provided.Value); // ← Safe or dangerous?
```

### Issue Found
| Issue | Status |
|-------|--------|
| Reference not configured | [Check Inspector] |
| .Value without null check | [yes/no] |
| Accessed before load complete | [yes/no] |
| Disposed before access | [yes/no] |

### Fix
```csharp
// Add null check
if (provided.Value != null)
{
    DoSomething(provided.Value);
}
```

### Unity Inspector Check
1. Open PluginSettingsContainer
2. Find the settings class
3. Verify `{{ASSET}}` field is assigned
```

## Execute
Investigate `{{ASSET}}` for `{{ISSUE}}`.
