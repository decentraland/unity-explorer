# Investigate Behavior - Sub-agent

You are tracing why a feature doesn't behave as expected in decentraland/unity-explorer.

## Parameters
- FEATURE: The feature/system that's misbehaving
- EXPECTED: What should happen
- ACTUAL: What actually happens
- TRIGGER: How to trigger the behavior

## Tasks

### 1. Find Feature Entry Point
```bash
rg "{{FEATURE}}" -n Explorer/Assets --type cs -l
rg "public.*{{FEATURE}}|void.*{{FEATURE}}" -n Explorer/Assets --type cs -B 2 -A 5
```

### 2. Trace Trigger Path
```bash
# Find what calls this feature
rg "{{TRIGGER}}" -n Explorer/Assets --type cs -B 3 -A 3
```

### 3. Find Conditional Logic
```bash
# Find if/else, switches that control behavior
rg "if\s*\(|switch\s*\(|else|case\s+" -n [FEATURE_FILES] --type cs
```

### 4. Check State/Flags
```bash
# Find booleans and state that affect behavior
rg "bool|isEnabled|isActive|state|flag|enabled" -n [FEATURE_FILES] --type cs
```

### 5. Find Event Handlers
```bash
# Find event subscriptions
rg "\.Subscribe|\.On|Event\s*\+|Action<|Func<|delegate" -n [FEATURE_FILES] --type cs
```

### 6. Check Guards/Early Returns
```bash
# Find conditions that might prevent execution
rg "return;|return\s+[^;]+;|continue;|break;" -n [FEATURE_FILES] --type cs -B 3
```

### 7. Find Related Systems
```bash
# Check if ECS systems are involved
rg "System.*Update|Update.*\(\)" -n [FEATURE_FILES] --type cs -A 10
```

### 8. Check Dependencies
```bash
# Find what this feature depends on
rg "private|readonly|inject" -n [FEATURE_FILES] --type cs | head -20
```

### 9. Find Configuration
```bash
# Find settings/config that affects behavior
rg "Settings|Config|Options|Preferences" -n [FEATURE_FILES] --type cs
```

## Output Format

```markdown
## Behavior Analysis: {{FEATURE}}

### Expected vs Actual
| Expected | Actual |
|----------|--------|
| {{EXPECTED}} | {{ACTUAL}} |

### Execution Path
```
1. [Trigger: {{TRIGGER}}]
   ↓
2. [Entry point: file:line]
   ↓
3. [Processing step]
   ↓
4. [Point where expected ≠ actual] ← DIVERGENCE
   ↓
5. [Actual outcome]
```

### Divergence Point
**Location**: [file:line]
**Reason**: [why behavior differs here]

### Contributing Conditions
| Condition | Location | Impact |
|-----------|----------|--------|
| [condition] | [file:line] | [how it affects behavior] |

### Guards/Early Returns Found
```csharp
// This might prevent expected behavior
[code showing guard condition]
```

### State Dependencies
| State/Flag | Current Value? | Expected Value |
|------------|---------------|----------------|
| [flag] | [unknown/check] | [what it should be] |

### Evidence
```csharp
// Code showing the divergence
[code]
```

### Why Behavior Differs
[Detailed explanation of why actual ≠ expected]

### To Verify
- [ ] Check [state/flag] value at runtime
- [ ] Add logging at [location]
- [ ] Verify [condition]

### Confidence: [High/Medium/Low]
```

## Execute

Trace why `{{FEATURE}}` does `{{ACTUAL}}` instead of `{{EXPECTED}}` when `{{TRIGGER}}`.
