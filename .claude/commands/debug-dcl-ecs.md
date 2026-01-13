# Investigate ECS - Sub-agent

You are investigating an ECS-related null reference in decentraland/unity-explorer.

## Parameters
- COMPONENT: The component type to trace
- ENTITY_CONTEXT: Where the entity comes from
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number

## DCL ECS Context

This project uses **Arch ECS** (not Unity DOTS):
- Components are structs or classes added to entities
- Systems run in groups with defined order
- Two worlds: Global (single) and Scene (per JS scene)
- `SingleInstanceEntity` pattern for unique entities
- `EntityReference` for safe cross-frame entity storage

## Investigation Tasks

### 1. Find Component Definition
```bash
rg "struct {{COMPONENT}}|class {{COMPONENT}}" -n Explorer/Assets --type cs -A 10
```
- Is it a struct (value type) or class (reference type)?
- What fields does it contain?
- Are any fields nullable?

### 2. Find Where Component Is Added
```bash
rg "\.Add\(.*{{COMPONENT}}|\.Add<{{COMPONENT}}|AddComponent.*{{COMPONENT}}" -n Explorer/Assets --type cs -B 3 -A 3
```
- Which system/method adds this component?
- Is it added conditionally?
- What are the prerequisites?

### 3. Find Where Component Is Queried
```bash
rg "Query<.*{{COMPONENT}}|\.Get<{{COMPONENT}}|\.TryGet<{{COMPONENT}}" -n Explorer/Assets --type cs -B 2 -A 2
```
- Which systems read this component?
- Do they use `Get<>` (throws) or `TryGet<>` (safe)?
- Is there a mismatch between add and query locations?

### 4. Find Where Component Is Removed
```bash
rg "\.Remove<{{COMPONENT}}|RemoveComponent.*{{COMPONENT}}" -n Explorer/Assets --type cs -B 2 -A 2
```
- Is the component removed before it's queried?
- Is removal conditional?

### 5. Check System Execution Order
```bash
rg "\[UpdateInGroup\(typeof\(|UpdateAfter\(typeof\(|UpdateBefore\(typeof\(" -n {{CRASH_FILE}} --type cs
rg "class.*System.*:.*BaseSystem|ComponentSystemBase" -n Explorer/Assets --type cs | head -20
```
- What SystemGroup is the crashing system in?
- What systems run before/after it?
- Could a dependency system not have run yet?

### 6. Check Entity Lifecycle
```bash
rg "EntityReference|SingleInstanceEntity|world\.Create\(|\.Destroy\(" -n Explorer/Assets --type cs | grep -i "{{COMPONENT}}\|{{ENTITY_CONTEXT}}"
```
- Is entity created before component is queried?
- Is entity destroyed prematurely?
- Is EntityReference used correctly?

### 7. Check World Context
```bash
rg "GlobalWorld|SceneWorld|ISceneFacade" -n {{CRASH_FILE}} --type cs
```
- Is this Global or Scene world?
- Could there be cross-world access issues?

## Output Format

```markdown
## ECS Investigation: {{COMPONENT}}

### Component Definition
- **Type**: [struct/class]
- **Location**: [file:line]
- **Fields**: [list nullable fields]

### Lifecycle Analysis
| Stage | Location | Conditional? |
|-------|----------|--------------|
| Created | [file:line] | [yes/no] |
| Added | [file:line] | [yes/no] |
| Queried | [file:line] | [yes/no] |
| Removed | [file:line] | [yes/no] |

### System Order
```
[Previous System] 
  ↓ (adds component?)
[Crashing System] ← queries component here
  ↓
[Next System]
```

### Issues Found
- [ ] Component added conditionally but queried unconditionally
- [ ] System order incorrect - queried before added
- [ ] Entity destroyed before query
- [ ] Using Get<> instead of TryGet<>
- [ ] Wrong world context

### Null Scenario
[Explain the specific conditions where component would be missing]

### Evidence
```csharp
[Key code snippets]
```

### Confidence: [High/Medium/Low]
```

## Execute

Search for `{{COMPONENT}}` through its full lifecycle in the DCL codebase and report findings.
