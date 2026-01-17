# Investigate Lifecycle - Sub-agent

You are investigating an entity/world lifecycle related null in decentraland/unity-explorer.

## Parameters
- ENTITY_OR_WORLD: The entity or world context
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number

## DCL Lifecycle Context

**Worlds:**
- **Global World**: Single instance, handles realm, scenes lifecycle, player, camera, avatars
- **Scene World**: Per JavaScript scene, fully independent, disposed when scene unloads
- Worlds are **fully independent** - can't reference entities from other worlds

**Entities:**
- `Entity` is just an integer - doesn't contain world reference
- `EntityReference` for safe cross-frame storage
- `SingleInstanceEntity` for unique entities (cached query pattern)

**Disposal:**
- Scene worlds disposed on scene unload
- Entities destroyed don't notify - stale references crash
- Plugins must cleanup in Dispose()

## Investigation Tasks

### 1. Find Entity/World Access at Crash
```bash
sed -n '{{CRASH_LINE-10}},{{CRASH_LINE+10}}p' {{CRASH_FILE}} | cat -n
```
- Is it accessing entity components?
- Is it querying a world?
- Is there an EntityReference being resolved?

### 2. Check Entity Creation
```bash
rg "world\.Create\(|\.Create\(\)|CreateEntity" -n Explorer/Assets --type cs | grep -i "{{ENTITY_OR_WORLD}}"
```
- Where is the entity created?
- Is creation conditional?
- Which world owns it?

### 3. Check Entity Destruction
```bash
rg "\.Destroy\(|DestroyEntity|world\.Destroy" -n Explorer/Assets --type cs | grep -i "{{ENTITY_OR_WORLD}}"
```
- Where is entity destroyed?
- Could it be destroyed before crash site?

### 4. Check EntityReference Usage
```bash
rg "EntityReference" -n {{CRASH_FILE}} --type cs -B 2 -A 2
rg "EntityReference" -n Explorer/Assets --type cs | grep -i "{{ENTITY_OR_WORLD}}"
```
- Is EntityReference used for cross-frame storage?
- Is raw Entity stored (dangerous)?

### 5. Check SingleInstanceEntity Pattern
```bash
rg "SingleInstanceEntity" -n {{CRASH_FILE}} --type cs -B 3 -A 3
rg "SingleInstanceEntity" -n Explorer/Assets --type cs | head -20
```
- Is this a single-instance entity?
- Is it cached properly?
- Could cache be stale?

### 6. Check World Context
```bash
rg "GlobalWorld|SceneWorld|ISceneFacade|sceneWorld|globalWorld" -n {{CRASH_FILE}} --type cs
```
- Is this Global or Scene world?
- Could there be cross-world access (forbidden)?

### 7. Check World Injection
```bash
rg "ArchSystemsWorldBuilder|InjectToWorld|GlobalPluginArguments" -n {{CRASH_FILE}} --type cs -B 3 -A 5
```
- Is world injected via ContinueInitialization?
- Could world be accessed before injection?

### 8. Check Disposal Order
```bash
rg "IDisposable|Dispose\(\)|OnDestroy" -n {{CRASH_FILE}} --type cs -A 10
rg "Dispose" -n Explorer/Assets --type cs | grep -i "{{ENTITY_OR_WORLD}}"
```
- Is there proper disposal implementation?
- Could access happen after disposal?

### 9. Check Scene Lifecycle
```bash
rg "ISceneFacade|SceneLoading|SceneUnloading|sceneLifecycle" -n Explorer/Assets --type cs | grep -i "{{ENTITY_OR_WORLD}}"
```
- Is entity tied to scene lifecycle?
- Could scene unload before access?

### 10. Find World/Entity References
```bash
rg "{{ENTITY_OR_WORLD}}" -n {{CRASH_FILE}} --type cs
```
- How many references exist?
- Are any stored outside ECS (bad practice)?

## Output Format

```markdown
## Lifecycle Investigation: {{ENTITY_OR_WORLD}}

### Context
- **Type**: [Entity / World / SingleInstanceEntity]
- **World Scope**: [Global / Scene / Unknown]
- **Storage**: [EntityReference / Raw Entity / SingleInstanceEntity cache]

### Lifecycle Flow
```
1. [Creation point - file:line]
   ↓
2. [Usage during lifetime]
   ↓  
3. [Destruction/Disposal - file:line]
   ↓
4. [Crash site - accessing after destruction?]
```

### World Analysis
| Aspect | Status |
|--------|--------|
| World Type | [Global / Scene] |
| World Injected | [yes / no / unknown] |
| Cross-world Access | [none / detected] |

### Entity Analysis
| Aspect | Status |
|--------|--------|
| Created | [file:line] |
| Reference Type | [EntityReference / raw / SingleInstance] |
| Destroyed | [file:line or N/A] |
| Stale Reference Risk | [High / Med / Low] |

### Disposal Chain
```
1. [First thing disposed]
2. [Second thing disposed]
...
N. [Entity/World accessed here - after disposal?]
```

### Issues Found
- [ ] Raw Entity stored instead of EntityReference
- [ ] SingleInstanceEntity cache not refreshed
- [ ] Entity accessed after destruction
- [ ] World accessed before injection
- [ ] Cross-world entity access attempted
- [ ] Scene unloaded but entity still referenced
- [ ] Disposal order incorrect

### Null Scenario
[Explain specific lifecycle timing that causes null]

### Evidence
```csharp
[Key code showing lifecycle issue]
```

### Recommended Fix
```csharp
// Use EntityReference for safe storage
private EntityReference _entityRef;

// Check validity before access
if (_entityRef.TryGetEntity(world, out var entity))
{
    // Safe to use entity
}

// Or for SingleInstanceEntity
if (_singleInstance.TryGetEntity(world, out var entity))
{
    // Safe to use
}
```

### Confidence: [High/Medium/Low]
```

## Execute

Trace the lifecycle of `{{ENTITY_OR_WORLD}}` and identify timing/disposal issues.
