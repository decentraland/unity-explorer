---
name: async-programming
description: "Async programming patterns with UniTask, cancellation tokens, and exception handling. Use when writing async code, handling CancellationTokenSource lifecycle, using SuppressToResultAsync, implementing detached UniTask/UniTaskVoid flows, or working with Result/EnumResult types for exception-free flow propagation."
user-invocable: false
---

# Async Programming Patterns

## Sources

- `docs/async-programming.md` — Async pattern guidelines for UniTask flows
- `docs/architecture-overview.md` — Result struct and exception-free flow details

---

## Core Rules

> Condensed rules are in CLAUDE.md §9. This skill provides expanded patterns and code examples not covered there.

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

## Dispose races: cancel, don't catch

A pending async operation must never race with `Dispose()`. The owner cancels its
`CancellationTokenSource` (`SafeCancelAndDispose()`, see Cancellation Token Management below)
before tearing anything down, so in-flight work exits through `OperationCanceledException`,
which the flow already handles. Catching `ObjectDisposedException` to hide a race is patching
the symptom: if it shows up, the fix is the missing cancellation in the owner's `Dispose()`,
not a new `catch`.

### Last-resort compromise: a race the runtime owns

Only when cancellation cannot prevent the race, because it happens inside a platform or
third-party layer the code does not control, may the exception be absorbed. Then:

- Absorb it **once, at the layer that owns the resource**, with a comment naming why
  cancellation is not enough. It is a documented exception, never a pattern to copy.
- Never re-catch the same race up the stack: re-catching it at every caller is the same
  defensive redundancy as the "Defensive null-checks against non-null declarations"
  anti-pattern in CLAUDE.md §11. Callers handle only their own concerns (typically
  `OperationCanceledException`).

Known instance: on Mono, `Dispose()` racing with an in-flight `WebSocket.CloseAsync` does not
surface as a bare `ObjectDisposedException`; it comes back as a `WebSocketException` whose
`InnerException` is that `ObjectDisposedException`, from inside the runtime's socket close.
`DCLWebSocket.CloseAsync` owns the socket, so it is the one place that absorbs that exact shape:

```csharp
try
{
    await ws.CloseAsync(statusType, description, cancellationToken);
}
catch (System.Net.WebSockets.WebSocketException e) when (e.InnerException is ObjectDisposedException)
{
    // Mono surfaces the Dispose() race inside the runtime as a WebSocketException wrapping the
    // ObjectDisposedException; cancellation cannot reach it, so the owning layer absorbs it here.
}
catch (System.Net.WebSockets.WebSocketException e)
{
    throw new WebSocketException(e);
}
```

Higher layers such as `ClientWebSocketApiImplementation.CloseAsync` must **not** re-catch
`ObjectDisposedException` (nor `e.InnerException is ObjectDisposedException`); they only absorb
`OperationCanceledException` for a close cancelled mid-flight.

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
