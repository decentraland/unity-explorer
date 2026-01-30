# Investigate Plugin/DI - Sub-agent

Investigate plugin initialization and dependency injection issues in decentraland/unity-explorer.

## Parameters
- SERVICE: The service, plugin, or dependency to investigate
- ISSUE: What's wrong (not initialized, null, etc.)

## DCL Plugin Quick Reference
- Plugins: `IDCLPlugin`, `DCLGlobalPluginBase<TSettings>`, `DCLWorldPluginBase<TSettings>`
- Containers: `StaticContainer` (shared), `DynamicWorldContainer` (global plugins)
- Settings: `IDCLPluginSettings` in `PluginSettingsContainer` ScriptableObject
- Assets: `AssetReferenceT<T>`, `ComponentReference<T>` → loaded via `IAssetsProvisioner`
- World injection: `ContinueInitialization` pattern - world available only in continuation

## Tasks

```bash
# 1. Find service/plugin definition
rg "class {{SERVICE}}|interface I{{SERVICE}}" -n Explorer/Assets --type cs -A 15

# 2. Find settings class
rg "{{SERVICE}}Settings|Settings.*{{SERVICE}}" -n Explorer/Assets --type cs -A 20

# 3. Find container registration
rg "{{SERVICE}}" -n Explorer/Assets --type cs | grep -i "container"

# 4. Find initialization
rg "InitializeAsync|InitializeAsyncInternal|ContinueInitialization" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"

# 5. Check asset provisioning
rg "ProvideMainAssetAsync|ProvidedAsset|ProvidedInstance" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"

# 6. Check world injection timing
rg "ArchSystemsWorldBuilder|GlobalPluginArguments" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"
```

## Output Format

```markdown
## Plugin/DI Analysis: {{SERVICE}}

### Registration
- **Type**: [Plugin / Service / Controller]
- **Container**: [Static / Dynamic / None found]
- **Settings**: [Settings class name or N/A]

### Initialization Flow
```
1. Container creates → 2. InitializeAsyncInternal → 3. ContinueInitialization (world injected)
```
**Issue at step**: [which step fails]

### Asset References
| Reference | Configured? |
|-----------|-------------|
| [ref name] | [Check Unity Inspector] |

### Issue Found
[Explanation: e.g., "Service accessed before ContinueInitialization called"]

### Fix
```csharp
[Code fix]
```

### Unity Inspector Check
- [ ] Open PluginSettingsContainer
- [ ] Find {{SERVICE}}Settings
- [ ] Verify all asset references assigned
```

## Execute
Investigate `{{SERVICE}}` for `{{ISSUE}}`.
