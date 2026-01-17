# Investigate Async/UniTask - Sub-agent

You are investigating an async/UniTask related null in decentraland/unity-explorer.

## Parameters
- METHOD_NAME: The async method or context
- CRASH_FILE: File where crash occurred
- CRASH_LINE: Line number

## DCL Async Context

- **UniTask**: Primary async library (not standard Task)
- **CancellationToken**: Passed through most async operations
- **AssetPromise**: ECS-friendly polling-based async pattern
- **Result pattern**: Exception-free flow using `Result.SuccessResult()` / `Result.ErrorResult()`
- **SuppressToResultAsync**: Converts exceptions to Result

## Investigation Tasks

### 1. Find Async Method Definition
```bash
rg "async.*{{METHOD_NAME}}|UniTask.*{{METHOD_NAME}}" -n Explorer/Assets --type cs -A 20
```
- Is it UniTask or Task?
- What does it return?
- Does it accept CancellationToken?

### 2. Check Cancellation Handling
```bash
rg "CancellationToken|\.IsCancellationRequested|ThrowIfCancellationRequested" -n {{CRASH_FILE}} --type cs -B 2 -A 2
```

**Safe pattern:**
```csharp
if (ct.IsCancellationRequested)
    return Result.CancelledResult();
```

**Dangerous pattern:**
```csharp
ct.ThrowIfCancellationRequested(); // Throws, might not be caught
```

### 3. Find All Await Points
```bash
rg "await " -n {{CRASH_FILE}} --type cs
```
- Are all awaits before the crash line?
- Could any await be skipped?
- Is there early return before await completes?

### 4. Check Result Pattern Usage
```bash
rg "Result\.|SuppressToResultAsync|SuccessResult|ErrorResult|CancelledResult" -n {{CRASH_FILE}} --type cs -B 2 -A 2
```
- Is this method using exception-free Result pattern?
- Is Result checked before using value?

### 5. Check AssetPromise Pattern
```bash
rg "AssetPromise|LoadSystemBase|TLoadingIntention" -n Explorer/Assets --type cs | grep -i "{{METHOD_NAME}}"
```
For ECS async:
```csharp
// Promise entity created, polled each frame until resolved
// Consumed when ready, entity destroyed
```
- Is promise consumed before resolved?

### 6. Find Exception Handling
```bash
rg "try|catch|finally" -n {{CRASH_FILE}} --type cs -A 5
rg "OperationCanceledException" -n {{CRASH_FILE}} --type cs
```
- Are cancellation exceptions caught?
- Is there proper cleanup in finally?

### 7. Check Continuation/Callback
```bash
rg "ContinueWith|\.Then|ContinueInitialization|callback|Action<|Func<" -n {{CRASH_FILE}} --type cs
```
- Is crash in a continuation?
- Could callback execute after disposal?

### 8. Check Race Conditions
```bash
rg "lock|Interlocked|volatile|concurrent" -n {{CRASH_FILE}} --type cs
```
- Are there shared resources?
- Could multiple async ops conflict?

### 9. Trace Async Call Chain
```bash
rg "{{METHOD_NAME}}" -n Explorer/Assets --type cs | grep -v "{{CRASH_FILE}}"
```
- Who calls this method?
- What do they do with the result?

## Output Format

```markdown
## Async Investigation: {{METHOD_NAME}}

### Method Signature
- **Location**: [file:line]
- **Return Type**: [UniTask / UniTask<T> / Task]
- **Cancellation**: [CancellationToken param? yes/no]

### Async Flow Analysis
```
1. [Caller invokes method]
   ↓
2. [await #1 at line X] - [what it awaits]
   ↓
3. [await #2 at line Y] - [what it awaits]
   ↓
4. [Crash point at line Z]
```

### Cancellation Handling
| Check Point | Handled? | Pattern |
|-------------|----------|---------|
| Before await #1 | [yes/no] | [IsCancellationRequested / ThrowIf / none] |
| After await #1 | [yes/no] | [...] |
| At crash site | [yes/no] | [...] |

### Exception Handling
- **Try/Catch**: [present/absent]
- **SuppressToResultAsync**: [used/not used]
- **OperationCanceledException**: [caught/not caught]

### Potential Issues
| Issue | Likelihood | Evidence |
|-------|------------|----------|
| Cancelled but result used | [High/Med/Low] | [code] |
| Exception not caught | [High/Med/Low] | [code] |
| Race condition | [High/Med/Low] | [code] |
| Promise consumed early | [High/Med/Low] | [code] |
| Continuation after disposal | [High/Med/Low] | [code] |

### Issues Found
- [ ] CancellationToken not checked after await
- [ ] Result used without checking Success
- [ ] ThrowIfCancellationRequested without catch
- [ ] Missing SuppressToResultAsync wrapper
- [ ] Callback executes on disposed object

### Null Scenario
[Explain how async flow leads to null at crash site]

### Evidence
```csharp
[Key code snippets]
```

### Recommended Fix
```csharp
// Add proper cancellation check
if (ct.IsCancellationRequested)
    return; // or Result.CancelledResult()

// Check result before use
var result = await SomeAsyncOp(ct);
if (!result.Success)
    return Result.ErrorResult(result.ErrorMessage);
```

### Confidence: [High/Medium/Low]
```

## Execute

Trace the async flow of `{{METHOD_NAME}}` and identify where cancellation or async timing could cause null.
