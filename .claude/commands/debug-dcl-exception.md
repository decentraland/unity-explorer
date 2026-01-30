# Investigate Exception - Sub-agent

You are tracing the source of an exception in decentraland/unity-explorer.

## Parameters
- CRASH_FILE: File where exception occurred
- CRASH_LINE: Line number
- EXCEPTION_TYPE: Type of exception (NullReference, InvalidOperation, etc.)

## Tasks

### 1. Read Crash Site
```bash
cat -n {{CRASH_FILE}} | sed -n '{{CRASH_LINE-20}},{{CRASH_LINE+10}}p'
```

### 2. Identify Null/Invalid Candidates

For **NullReferenceException**, find expressions that could be null:
- `object.Property` → object could be null
- `Method().Result` → method return could be null
- `array[i]` → array could be null
- `entity.Get<T>()` → component might not exist

For **InvalidOperationException**, find invalid state:
- Collection modified during iteration
- Operation on disposed object
- Invalid state transition

For **ArgumentException/ArgumentNullException**:
- Trace parameter values to their source

### 3. Trace Each Candidate

For each null candidate:
```bash
# Find assignments
rg "[CANDIDATE]\s*=" -n Explorer/Assets --type cs

# Find where it could become null
rg "[CANDIDATE].*null|null.*[CANDIDATE]" -n Explorer/Assets --type cs
```

### 4. Check Initialization
```bash
# Constructor/init
rg "constructor|Initialize|Awake|Start|OnEnable" -n {{CRASH_FILE}} --type cs -A 10
```

### 5. Check Lifecycle
```bash
# Disposal/destruction
rg "Dispose|Destroy|OnDisable|OnDestroy" -n {{CRASH_FILE}} --type cs -A 5
```

### 6. Check Conditionals
```bash
# Find conditional assignments
rg "if.*{{CANDIDATE}}|{{CANDIDATE}}.*\?" -n {{CRASH_FILE}} --type cs -B 2 -A 2
```

### 7. DCL-Specific Checks

**For ECS (entity.Get<>):**
```bash
rg "\.Get<|\.TryGet<|\.Add\(|\.Remove<" -n {{CRASH_FILE}} --type cs
```

**For Assets (.Value access):**
```bash
rg "\.Value|ProvidedAsset|ProvidedInstance" -n {{CRASH_FILE}} --type cs
```

**For Async (cancelled operations):**
```bash
rg "CancellationToken|await|UniTask" -n {{CRASH_FILE}} --type cs
```

## Output Format

```markdown
## Exception Analysis: {{EXCEPTION_TYPE}}

### Crash Site
**File**: {{CRASH_FILE}}:{{CRASH_LINE}}
```csharp
[Code at crash line with context]
```

### Null/Invalid Candidates
| Expression | Likelihood | Reason |
|------------|------------|--------|
| [expr1] | [High/Med/Low] | [why it might be null] |

### Most Likely Cause
**Expression**: `[the null expression]`
**Origin**: [where it comes from]

### Trace
```
1. [Where value should be set]
   ↓
2. [What could skip/nullify it]
   ↓
3. [Crash: value is null at access]
```

### Evidence
```csharp
// Where null originates
[code]

// Where null is accessed
[code]
```

### Why It's Null
[Detailed explanation of the condition/path that leads to null]

### Confidence: [High/Medium/Low]
```

## Execute

Analyze the {{EXCEPTION_TYPE}} at {{CRASH_FILE}}:{{CRASH_LINE}} and identify the source.
