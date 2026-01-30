# Investigate Performance - Sub-agent

You are identifying performance issues in decentraland/unity-explorer.

## Parameters
- SYMPTOM: The performance problem (lag, memory, freeze, etc.)
- CONTEXT: When it occurs
- AFFECTED_AREA: System/feature/scene affected

## Tasks

### 1. Find Update Loops
```bash
rg "void Update|void LateUpdate|void FixedUpdate|override.*Update" -n Explorer/Assets --type cs | grep -i "{{AFFECTED_AREA}}"
```

### 2. Find ECS System Updates
```bash
rg "class.*System.*:.*BaseSystem|ComponentSystemBase" -n Explorer/Assets --type cs | grep -i "{{AFFECTED_AREA}}"
rg "public override void Update" -n Explorer/Assets --type cs | grep -i "{{AFFECTED_AREA}}"
```

### 3. Check for Allocations in Hot Paths
```bash
# new keyword in update loops
rg "new\s+[A-Z]|\.ToList\(\)|\.ToArray\(\)|string\s*\+" -n [AFFECTED_FILES] --type cs

# LINQ in update loops (allocates)
rg "\.Select\(|\.Where\(|\.OrderBy\(|\.GroupBy\(" -n [AFFECTED_FILES] --type cs
```

### 4. Find Heavy Operations
```bash
# Expensive operations
rg "GetComponent|Find\(|FindObjectOfType|Resources\.Load|Instantiate\(" -n [AFFECTED_FILES] --type cs

# Physics
rg "Raycast|OverlapSphere|SphereCast" -n [AFFECTED_FILES] --type cs
```

### 5. Check Queries
```bash
# ECS queries (can be expensive)
rg "Query<|\.Query\(|world\.Query" -n [AFFECTED_FILES] --type cs
```

### 6. Find Iteration Over Collections
```bash
rg "foreach|for\s*\(|\.ForEach\(" -n [AFFECTED_FILES] --type cs -A 3
```

### 7. Check Caching
```bash
# Look for missing caching
rg "private.*=.*Get|Cache|cached|_cached" -n [AFFECTED_FILES] --type cs
```

### 8. Find Async/Coroutine Issues
```bash
# Check for async/coroutine patterns
rg "StartCoroutine|async|UniTask|yield return" -n [AFFECTED_FILES] --type cs -B 2 -A 2
```

### 9. Check for Redundant Work
```bash
# Same operation multiple times
rg "\.Count|\.Length" -n [AFFECTED_FILES] --type cs | sort | uniq -d
```

### 10. Memory Concerns
```bash
# Large allocations, leaks
rg "List<|Dictionary<|new.*\[\]|StringBuilder" -n [AFFECTED_FILES] --type cs
rg "Dispose|IDisposable" -n [AFFECTED_FILES] --type cs
```

## Output Format

```markdown
## Performance Analysis: {{SYMPTOM}}

### Affected Area
- **Systems/Files**: [list]
- **Context**: {{CONTEXT}}

### Hotspots Found

#### Update Loop Issues
| Location | Issue | Impact |
|----------|-------|--------|
| [file:line] | [what's expensive] | [High/Med/Low] |

#### Allocations in Hot Paths
```csharp
// Allocation in Update - BAD
[code example]
```

#### Heavy Operations
| Operation | Location | Frequency |
|-----------|----------|-----------|
| [operation] | [file:line] | [per frame/often/rare] |

### Missing Optimizations
| What | Where | Recommendation |
|------|-------|----------------|
| [missing cache] | [file:line] | [cache this] |

### Expensive Patterns Found
```csharp
// Example of expensive pattern
[code]

// Should be
[optimized code]
```

### Recommendations

#### Quick Wins
1. [Easy optimization with big impact]
2. [Another quick fix]

#### Architectural Improvements
1. [Larger refactor for performance]
2. [Caching strategy]

### Estimated Impact
| Fix | Effort | Impact |
|-----|--------|--------|
| [fix 1] | [Low/Med/High] | [Low/Med/High] |

### Profiling Suggestions
- Add profiler marker at [location]
- Check memory at [point]
- Measure frame time for [operation]

### Confidence: [High/Medium/Low]
```

## Execute

Identify performance issues causing `{{SYMPTOM}}` in `{{AFFECTED_AREA}}` during `{{CONTEXT}}`.
