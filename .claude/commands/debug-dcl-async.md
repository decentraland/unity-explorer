# Investigate Async - Sub-agent

Investigate async/UniTask issues in decentraland/unity-explorer.

## Parameters
- METHOD: The async method or flow to investigate
- ISSUE: What's wrong (cancelled, not awaited, result not checked, etc.)

## DCL Async Quick Reference
- Uses **UniTask** (not standard Task)
- `CancellationToken` passed through most async ops
- `Result` pattern: `Result.SuccessResult()`, `Result.ErrorResult()`, `Result.CancelledResult()`
- `SuppressToResultAsync` converts exceptions to Result
- `AssetPromise<T, TIntent>` for ECS polling-based async
- **Never use** `ThrowIfCancellationRequested()` without catch

## Tasks

```bash
# 1. Find async method
rg "async.*{{METHOD}}|UniTask.*{{METHOD}}" -n Explorer/Assets --type cs -A 15

# 2. Find await points
rg "await\s+" -n [METHOD_FILE] --type cs

# 3. Check cancellation handling
rg "CancellationToken|IsCancellationRequested" -n [METHOD_FILE] --type cs -B 1 -A 2

# 4. Check Result pattern
rg "Result\.|SuppressToResultAsync|SuccessResult|ErrorResult" -n [METHOD_FILE] --type cs

# 5. Find exception handling
rg "try|catch|OperationCanceledException" -n [METHOD_FILE] --type cs -A 3

# 6. Check callers
rg "{{METHOD}}" -n Explorer/Assets --type cs | grep -v "{{METHOD_FILE}}"
```

## Output Format

```markdown
## Async Analysis: {{METHOD}}

### Method Signature
```csharp
[method signature with return type and params]
```

### Async Flow
```
1. [await #1] - [what]
2. [await #2] - [what]
3. [result used] ← Issue here?
```

### Cancellation Handling
| Check Point | Handled? |
|-------------|----------|
| Before await | [yes/no] |
| After await | [yes/no] |
| Before using result | [yes/no] |

### Issue Found
[Explanation: e.g., "CancellationToken not checked after await, result used when cancelled"]

### Fix
```csharp
// Check cancellation after await
var result = await SomeOperation(ct);
if (ct.IsCancellationRequested)
    return Result.CancelledResult();

// Or use SuppressToResultAsync
var result = await InternalOp(ct).SuppressToResultAsync();
if (!result.Success)
    return result;
```
```

## Execute
Investigate `{{METHOD}}` for `{{ISSUE}}`.
