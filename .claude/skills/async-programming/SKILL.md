---
name: async-programming
description: "Async programming patterns with UniTask, cancellation tokens, and exception handling. Use when writing async code, handling CancellationTokenSource lifecycle, using SuppressToResultAsync, or implementing detached UniTask/UniTaskVoid flows."
user-invocable: false
---

# Async Programming Patterns

## Sources

- `docs/async-programming.md` — Async pattern guidelines for UniTask flows
- `docs/architecture-overview.md` — Result struct and exception-free flow details

---

## Core Rules

1. **Minimize detached `UniTask` / `UniTaskVoid` calls.** A detached flow is a `UniTaskVoid` or `.Forget()`-ed `UniTask` — it creates a heap-allocated delegate disconnected from its origin. Prefer parent async flow when possible.

2. **Always catch exceptions in detached flows:**
   - Ignore `OperationCanceledException` (normal cancellation)
   - Report all others via `ReportHub.LogException`

3. **Use `SuppressToResultAsync()`** to convert exceptions into `Result` values cleanly.

4. **Handle cancellation with `ct.IsCancellationRequested`**, never `ThrowIfCancellationRequested()` — exceptions are expensive.

## Exception Handling Pattern

### Wrong — Unhandled exceptions in detached flow

```csharp
// BAD: Exception kills the flow silently
async UniTaskVoid DoSomethingAsync(CancellationToken ct)
{
    var result = await webRequestController.GetAsync(args, ct);
    // If this throws, nobody catches it
}
```

### Correct — Proper exception handling

```csharp
async UniTaskVoid DoSomethingAsync(CancellationToken ct)
{
    try
    {
        var result = await webRequestController.GetAsync(args, ct);
        ProcessResult(result);
    }
    catch (OperationCanceledException) { }  // Normal cancellation — ignore
    catch (Exception exception)
    {
        ReportHub.LogException(exception, ReportCategory.GENERIC_WEB_REQUEST);
    }
}
```

### Code Example — Exception Handling in Controller

From `MinimapController.cs`:

```csharp
private async UniTask<PlacesData.PlaceInfo?> GetPlaceInfoAsync(
    Vector2Int parcelPosition, CancellationToken ct, bool renewCache = false)
{
    try
    {
        return await placesAPIService.GetPlaceAsync(parcelPosition, ct, renewCache);
    }
    catch (OperationCanceledException _) { }
    catch (NotAPlaceException notAPlaceException)
    {
        ReportHub.LogWarning(ReportCategory.UNSPECIFIED,
            $"Not a place requested: {notAPlaceException.Message}");
    }
    catch (Exception exception)
    {
        ReportHub.LogException(exception, ReportCategory.GENERIC_WEB_REQUEST);
    }

    return null;
}
```

## SuppressToResultAsync Pattern

Wraps a `UniTask<T>` in a try/catch and returns a `Result<T>` struct instead of throwing.

### Code Example — SuppressToResultAsync + Result

From `TokenFileAuthenticator.cs`:

```csharp
private async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
{
    if (!File.Exists(TOKEN_PATH))
        throw new AutoLoginTokenNotFoundException();

    // SuppressToResultAsync catches exceptions and wraps in Result<string>
    Result<string> contentResult = await File.ReadAllTextAsync(TOKEN_PATH, ct)!
        .SuppressToResultAsync<string>(ReportCategory.AUTHENTICATION);

    if (contentResult.Success == false)
        throw new Exception(contentResult.ErrorMessage ?? "Cannot read token file");

    string token = contentResult.Value;
    // ... continue with token ...
}
```

## Result and EnumResult Types

### Result Struct

Zero-cost value type for exception-free flow propagation:

```csharp
// Success
return Result<string>.SuccessResult(value);

// Error
return Result<string>.ErrorResult("Something went wrong");

// Check
if (result.Success)
    UseValue(result.Value);
else
    HandleError(result.ErrorMessage);
```

### REnum Union Types

Roslyn Source Generator for Rust-style enums. Zero-cost pattern matching:

```csharp
// Pattern matching via .Match()
result.Match(
    success: value => ProcessValue(value),
    error: msg => LogError(msg)
);

// Safe access via .IsXXX(out T)
if (result.IsSuccess(out var value))
    ProcessValue(value);
```

## Cancellation Token Management

### CancellationTokenSource Lifecycle

From `MinimapController.cs` — proper CTS management:

```csharp
private CancellationTokenSource? placesApiCts;

protected override void OnFocus()
{
    // Cancel any in-flight request
    placesApiCts.SafeCancelAndDispose();
    placesApiCts = new CancellationTokenSource();
    RefreshPlaceInfoUIAsync(previousParcelPosition, placesApiCts.Token).Forget();
}

// SafeRestart = cancel + dispose + create new
private void OnFavoriteButtonClicked(bool value)
{
    favoriteCancellationToken = favoriteCancellationToken.SafeRestart();
    SetAsFavoriteAsync(favoriteCancellationToken.Token).Forget();
}

public override void Dispose()
{
    placesApiCts.SafeCancelAndDispose();
    disposeCts.Cancel();
    favoriteCancellationToken.SafeCancelAndDispose();
}
```

**Key patterns:**
- `SafeCancelAndDispose()` — Cancel and dispose in one call, null-safe
- `SafeRestart()` — Cancel + dispose + create new CTS
- Always dispose CTS in `Dispose()`
- Use a dedicated `disposeCts` for operations that should cancel on controller disposal

### Cancellation Checking

```csharp
// CORRECT — Check without throwing (cheap)
if (ct.IsCancellationRequested)
    return;

// WRONG — Throws exception (expensive)
ct.ThrowIfCancellationRequested();
```
