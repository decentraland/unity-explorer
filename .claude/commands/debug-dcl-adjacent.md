# Investigate Adjacent Code - Sub-agent

You are reviewing code adjacent to a crash site in decentraland/unity-explorer.

## Parameters
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number
- CRASH_DIR: Directory containing crash file

## Investigation Tasks

### 1. Read Full Crash File
```bash
cat -n {{CRASH_FILE}}
```
- Understand the full context of the class
- Note initialization, dependencies, disposal

### 2. Find Related Systems in Same Group
```bash
rg "\[UpdateInGroup\(" -n {{CRASH_DIR}} --type cs -A 1
```
- What SystemGroup is this in?
- What other systems are in the same group?

### 3. Check System Dependencies
```bash
rg "\[UpdateAfter\(typeof|UpdateBefore\(typeof" -n {{CRASH_DIR}} --type cs
```
- What systems must run before this one?
- What systems run after?

### 4. Find Similar Patterns
```bash
# Find similar null-prone patterns in same directory
rg "\.Get<|\.Value|FirstOrDefault|SingleOrDefault" -n {{CRASH_DIR}} --type cs
```
- Are there similar access patterns?
- Do others have null checks?

### 5. Check for Defensive Patterns
```bash
rg "TryGet|\.HasValue|\?\.|!= null|== null" -n {{CRASH_DIR}} --type cs
```
- Is defensive coding used elsewhere?
- Is crash site missing these patterns?

### 6. Look for TODO/FIXME Comments
```bash
rg "TODO|FIXME|HACK|BUG|XXX" -n {{CRASH_FILE}} --type cs
rg "TODO|FIXME|HACK|BUG|XXX" -n {{CRASH_DIR}} --type cs
```
- Any known issues flagged?
- Any incomplete implementations?

### 7. Check Git History (if available)
```bash
git log --oneline -10 -- {{CRASH_FILE}} 2>/dev/null || echo "Git not available"
git blame -L {{CRASH_LINE-5}},{{CRASH_LINE+5}} {{CRASH_FILE}} 2>/dev/null || echo "Git blame not available"
```
- Recent changes to this file?
- Who last modified crash area?

### 8. Find Test Coverage
```bash
rg "$(basename {{CRASH_FILE}} .cs)" -n Explorer/Assets --type cs | grep -i "test"
```
- Are there tests for this code?
- Do tests cover the null scenario?

### 9. Check Error Handling in File
```bash
rg "try|catch|throw|Exception" -n {{CRASH_FILE}} --type cs
```
- Is there error handling?
- Should there be more?

### 10. Find Interface/Base Class
```bash
head -50 {{CRASH_FILE}} | grep -E "class.*:|interface"
rg "abstract|virtual|override" -n {{CRASH_FILE}} --type cs
```
- What does this class inherit from?
- Are there virtual methods that might behave differently?

### 11. Check Parallel/Similar Implementations
```bash
# Find files with similar names
ls -la {{CRASH_DIR}}/*.cs 2>/dev/null
# Find similar class patterns
rg "class.*$(basename {{CRASH_FILE}} .cs | sed 's/System//')" -n Explorer/Assets --type cs -l
```
- Are there similar systems/classes?
- How do they handle nulls?

## Output Format

```markdown
## Adjacent Code Review: {{CRASH_FILE}}

### File Overview
- **Class Name**: [name]
- **Type**: [System / Plugin / Controller / Service]
- **Base Class**: [if any]
- **SystemGroup**: [if applicable]

### Related Code
| File | Relationship | Null Handling |
|------|--------------|---------------|
| [file1] | [same group / depends on / depended by] | [defensive / not defensive] |

### Patterns Analysis
| Pattern | In Crash File | In Adjacent Files |
|---------|---------------|-------------------|
| TryGet<> | [used/not used] | [used/not used] |
| Null checks | [present/absent] | [present/absent] |
| ?. operator | [used/not used] | [used/not used] |

### Code Quality Indicators
- **TODO/FIXME found**: [yes/no - list if yes]
- **Error handling**: [comprehensive / partial / none]
- **Test coverage**: [found / not found]
- **Recent changes**: [yes - summary / no / unknown]

### Similar Patterns Found
```csharp
// From adjacent file that handles nulls properly
[example of defensive pattern]
```

```csharp
// From crash file that lacks defense
[example of vulnerable pattern]
```

### Issues Found
- [ ] Inconsistent null handling compared to similar code
- [ ] Missing defensive patterns used elsewhere
- [ ] TODO/FIXME indicates known issue
- [ ] Recent changes may have introduced bug
- [ ] No test coverage for null scenario

### Recommendations
1. [Specific recommendation based on findings]
2. [Additional pattern to apply]

### Files to Also Review
| File | Reason |
|------|--------|
| [file] | [why relevant] |

### Confidence: [High/Medium/Low]
```

## Execute

Review `{{CRASH_FILE}}` and adjacent code to find patterns and potential related issues.
