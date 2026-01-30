# Investigate Related Code - Sub-agent

Review code adjacent to the problem area in decentraland/unity-explorer.

## Parameters
- FILE: The file to review
- AREA: The broader area/feature
- ISSUE_TYPE: Type of issue being investigated

## Tasks

```bash
# 1. Read the file
cat -n {{FILE}}

# 2. Find similar files
ls -la $(dirname {{FILE}})/*.cs 2>/dev/null

# 3. Find similar patterns in codebase
rg "$(basename {{FILE}} .cs | sed 's/System\|Controller\|Plugin//')" -n Explorer/Assets --type cs -l | head -10

# 4. Check for defensive coding patterns
rg "TryGet|\?\.|!= null|== null" -n {{FILE}} --type cs

# 5. Find TODO/FIXME
rg "TODO|FIXME|HACK|BUG" -n {{FILE}} --type cs
rg "TODO|FIXME|HACK|BUG" -n $(dirname {{FILE}}) --type cs

# 6. Check git history (if available)
git log --oneline -5 -- {{FILE}} 2>/dev/null || echo "Git not available"

# 7. Find tests
rg "$(basename {{FILE}} .cs)" -n Explorer/Assets --type cs | grep -i test
```

## Output Format

```markdown
## Related Code Analysis: {{FILE}}

### File Overview
- **Class**: [name]
- **Type**: [System / Plugin / Controller / etc.]
- **Related Files**: [list similar files]

### Patterns Found
| Pattern | Present? | Example |
|---------|----------|---------|
| Null checks | [yes/no] | [code] |
| TryGet usage | [yes/no] | [code] |
| Error handling | [yes/no] | [code] |

### Issues/Notes
- [ ] TODO/FIXME found: [details]
- [ ] Missing defensive code at: [location]
- [ ] Similar code handles this better: [file]

### Similar Code Reference
```csharp
// From [other file] - handles this well
[good pattern example]
```

### Recommendations
1. [Recommendation based on findings]
```

## Execute
Review `{{FILE}}` and related code for `{{ISSUE_TYPE}}` issues in `{{AREA}}`.
