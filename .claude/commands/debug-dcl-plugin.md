# Investigate Plugin/DI - Sub-agent

You are investigating a Plugin or Dependency Injection related null in decentraland/unity-explorer.

## Parameters
- SERVICE: The service/plugin/dependency name
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number

## DCL Plugin Architecture

- **Plugins**: `IDCLPlugin`, `DCLGlobalPluginBase<TSettings>`, `DCLWorldPluginBase<TSettings>`
- **Containers**: `StaticContainer` (shared deps), `DynamicWorldContainer` (global plugins)
- **Settings**: `IDCLPluginSettings` stored in `PluginSettingsContainer` ScriptableObject
- **Asset References**: `AssetReferenceT<T>`, `ComponentReference<T>` for Addressables
- **Provisioning**: `IAssetsProvisioner` loads assets, returns `ProvidedAsset<T>`

## Investigation Tasks

### 1. Find Service/Plugin Definition
```bash
rg "class {{SERVICE}}|interface I{{SERVICE}}" -n Explorer/Assets --type cs -A 20
```
- Is it a plugin, service, or controller?
- What does it depend on?

### 2. Find Plugin Settings
```bash
rg "{{SERVICE}}Settings|class.*Settings.*{{SERVICE}}" -n Explorer/Assets --type cs -A 30
```
- Does it have IDCLPluginSettings?
- What AssetReferences does it need?
- Are there ComponentReferences?

### 3. Check Container Registration
```bash
rg "{{SERVICE}}" -n Explorer/Assets --type cs | grep -i "container\|static\|dynamic"
rg "StaticContainer|DynamicWorldContainer" -n Explorer/Assets --type cs -A 50 | grep -i "{{SERVICE}}"
```
- Where is it created/registered?
- What scope (static, global, scene)?

### 4. Check Plugin Initialization
```bash
rg "InitializeAsync|InitializeAsyncInternal|ContinueInitialization" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"
```
- Is initialization async?
- Does it use ContinueInitialization pattern?
- Is world injection required?

### 5. Find Asset Provisioning
```bash
rg "ProvideMainAssetAsync|ProvidedAsset|ProvidedInstance" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"
rg "assetsProvisioner\." -n {{CRASH_FILE}} --type cs -B 2 -A 5
```
- Are assets loaded before use?
- Is `.Value` accessed safely?

### 6. Check World Injection Timing
```bash
rg "ArchSystemsWorldBuilder|GlobalPluginArguments|InjectToWorld" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"
```
For global plugins:
```csharp
// Pattern: World available only in continuation
return (ref ArchSystemsWorldBuilder<World> world, in GlobalPluginArguments _) =>
{
    // Safe to use world here
};
```
- Is service used before world injection?

### 7. Check Disposal
```bash
rg "Dispose|IDisposable" -n Explorer/Assets --type cs | grep -i "{{SERVICE}}"
```
- Is service disposed prematurely?
- Is there cleanup order issue?

### 8. Find Usage Site
```bash
rg "{{SERVICE}}" -n {{CRASH_FILE}} --type cs -B 5 -A 5
```
- How is it accessed at crash site?
- Is it injected via constructor?
- Is it accessed statically?

## Output Format

```markdown
## Plugin/DI Investigation: {{SERVICE}}

### Service Definition
- **Type**: [Plugin / Service / Controller]
- **Location**: [file:line]
- **Base Class**: [DCLGlobalPluginBase / DCLWorldPluginBase / none]

### Dependencies
| Dependency | Source | Nullable? |
|------------|--------|-----------|
| [dep1] | [container/injection] | [yes/no] |

### Settings Configuration
- **Settings Class**: [name or N/A]
- **Asset References**:
  | Reference | Type | Configured? |
  |-----------|------|-------------|
  | [ref1] | AssetReferenceT<X> | [check Unity Inspector] |

### Initialization Flow
```
1. [Container creates service]
   ↓
2. [InitializeAsyncInternal loads assets]
   ↓
3. [ContinueInitialization injects world] ← Is crash before this?
   ↓
4. [Service ready for use]
```

### Issues Found
- [ ] Service used before initialization complete
- [ ] Asset reference not configured in PluginSettingsContainer
- [ ] World accessed before injection
- [ ] Dependency not registered in container
- [ ] Disposed before consumer finished

### Null Scenario
[Explain specific conditions where service/dependency would be null]

### Evidence
```csharp
[Key code snippets]
```

### Action Required
- [ ] Check PluginSettingsContainer in Unity Inspector
- [ ] Verify asset references assigned
- [ ] Check initialization order

### Confidence: [High/Medium/Low]
```

## Execute

Trace `{{SERVICE}}` through DCL's plugin and DI system and report findings.
