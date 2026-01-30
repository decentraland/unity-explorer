# Investigate ECS - Sub-agent

Investigate ECS-related issues (components, entities, systems, queries) in decentraland/unity-explorer.

## Parameters
- TARGET: Component, Entity, or System to investigate
- ISSUE: What's wrong (missing component, wrong order, etc.)

## DCL ECS Quick Reference
- Uses **Arch ECS** (not Unity DOTS)
- `entity.Get<T>()` throws if component missing
- `entity.TryGet<T>(out var c)` is safe
- Systems run in groups with `[UpdateInGroup]`, `[UpdateAfter]`, `[UpdateBefore]`
- Two worlds: Global (single) and Scene (per JS scene)
- `SingleInstanceEntity` for unique entities
- `EntityReference` for safe cross-frame storage

## Tasks

```bash
# 1. Find component/system definition
rg "struct {{TARGET}}|class {{TARGET}}|interface {{TARGET}}" -n Explorer/Assets --type cs -A 10

# 2. Find where component is added
rg "\.Add\(.*{{TARGET}}|\.Add<{{TARGET}}|Has<{{TARGET}}" -n Explorer/Assets --type cs -B 2 -A 2

# 3. Find where component is queried
rg "Query<.*{{TARGET}}|\.Get<{{TARGET}}|\.TryGet<{{TARGET}}" -n Explorer/Assets --type cs -B 2 -A 2

# 4. Find where component is removed
rg "\.Remove<{{TARGET}}|Remove.*{{TARGET}}" -n Explorer/Assets --type cs

# 5. Check system execution order
rg "\[UpdateInGroup\(|\[UpdateAfter\(|\[UpdateBefore\(" -n Explorer/Assets --type cs | grep -i "{{TARGET}}"

# 6. Check world context
rg "GlobalWorld|SceneWorld" -n Explorer/Assets --type cs | grep -i "{{TARGET}}"
```

## Output Format

```markdown
## ECS Analysis: {{TARGET}}

### Lifecycle
| Stage | Location | Conditional? |
|-------|----------|--------------|
| Defined | [file:line] | - |
| Added | [file:line] | [yes/no] |
| Queried | [file:line] | - |
| Removed | [file:line] | [yes/no] |

### System Order
```
[PreviousSystem] → [TargetSystem] → [NextSystem]
```

### Issue Found
[Explanation: e.g., "Component queried in System A but only added in System B which runs after"]

### Fix
```csharp
[Code fix]
```
```

## Execute
Investigate `{{TARGET}}` for `{{ISSUE}}`.
