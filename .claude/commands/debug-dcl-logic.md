# Investigate Logic - Sub-agent

You are tracing incorrect values or calculations in decentraland/unity-explorer.

## Parameters
- VALUE_NAME: The value that's wrong
- EXPECTED: What the value should be
- ACTUAL: What the value actually is
- CONTEXT: Where/when the value is computed

## Tasks

### 1. Find Value Definition
```bash
rg "{{VALUE_NAME}}" -n Explorer/Assets --type cs | head -30
```

### 2. Find Where Value Is Computed
```bash
rg "{{VALUE_NAME}}\s*=" -n Explorer/Assets --type cs -B 2 -A 5
```

### 3. Trace Inputs to Calculation
```bash
# Find what feeds into this value
cat -n [COMPUTATION_FILE] | grep -E "{{VALUE_NAME}}.*=|=.*{{VALUE_NAME}}"
```

### 4. Check Mathematical Operations
```bash
# Find arithmetic that might be wrong
rg "\+|\-|\*|\/|\%" -n [COMPUTATION_FILE] --type cs | grep -i "{{VALUE_NAME}}"
```

### 5. Check Type Conversions
```bash
# Find casts that might lose precision
rg "\(int\)|\(float\)|Convert\.|Parse\(" -n [COMPUTATION_FILE] --type cs
```

### 6. Check Order of Operations
```bash
# Complex expressions
rg "{{VALUE_NAME}}" -n [COMPUTATION_FILE] --type cs -A 2 -B 2
```

### 7. Find Conditionals Affecting Value
```bash
rg "if.*{{VALUE_NAME}}|{{VALUE_NAME}}.*\?" -n [COMPUTATION_FILE] --type cs -B 3 -A 5
```

### 8. Check for Off-by-One
```bash
# Array access, loops
rg "\[.*\]|< |<= |> |>= |\.Count|\.Length" -n [COMPUTATION_FILE] --type cs | grep -i "{{VALUE_NAME}}"
```

### 9. Check Unit/Scale Issues
```bash
# Unit conversions, scaling
rg "scale|Scale|factor|Factor|multiplier|ratio" -n [COMPUTATION_FILE] --type cs
```

### 10. Find Related Tests
```bash
rg "{{VALUE_NAME}}" -n Explorer/Assets --type cs | grep -i "test"
```

## Output Format

```markdown
## Logic Analysis: {{VALUE_NAME}}

### Value Discrepancy
| Expected | Actual |
|----------|--------|
| {{EXPECTED}} | {{ACTUAL}} |

### Computation Location
**File**: [file:line]
```csharp
[Code where value is computed]
```

### Input Trace
```
[Input A] = [value/source]
     ↓
[Input B] = [value/source]
     ↓
[Computation] = [formula]
     ↓
[Result] = {{ACTUAL}} (expected: {{EXPECTED}})
```

### Inputs Analysis
| Input | Source | Expected | Actual | Issue? |
|-------|--------|----------|--------|--------|
| [input1] | [where from] | [value] | [value] | [yes/no] |

### Computation Analysis
```csharp
// Current computation
{{VALUE_NAME}} = [current formula];

// Expected computation
{{VALUE_NAME}} = [correct formula];
```

### Issues Found

#### Most Likely Cause
[Detailed explanation of the error]

#### Other Potential Issues
| Issue | Location | Likelihood |
|-------|----------|------------|
| [off-by-one] | [file:line] | [High/Med/Low] |
| [wrong operator] | [file:line] | [High/Med/Low] |

### Evidence
```csharp
// The problematic code
[code with issue highlighted]
```

### Fix
```csharp
// Before
[incorrect code]

// After
[corrected code]
```

### Verification
- [ ] Add logging: `Debug.Log($"{{VALUE_NAME}} = {value}, inputs: {a}, {b}")`
- [ ] Add unit test for this calculation
- [ ] Check edge cases: [list edge cases]

### Confidence: [High/Medium/Low]
```

## Execute

Trace why `{{VALUE_NAME}}` is `{{ACTUAL}}` instead of `{{EXPECTED}}` in context `{{CONTEXT}}`.
