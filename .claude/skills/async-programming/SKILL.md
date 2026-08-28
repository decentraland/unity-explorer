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

## Swallowing teardown / dispose-race exceptions

During scene teardown a pending async operation can race with `Dispose()`. Swallow the
expected race **once, at the layer that owns the resource**, never defensively at every
caller. Re-catching the same race up the stack is noise (see the "Defensive null-checks
against non-null declarations" anti-pattern in CLAUDE.md §11): once the owning layer absorbs
it, callers only handle their own concerns (typically `OperationCanceledException`).

### Mono gotcha: WebSocketException wrapping ObjectDisposedException

On Mono, a `Dispose()` racing with an in-flight `WebSocket.CloseAsync` does **not** surface as
a bare `ObjectDisposedException`; it comes back as a `WebSocketException` whose
`InnerException` is that `ObjectDisposedException`. Catch that exact shape at the owning layer
(`DCLWebSocket`), so the race is fully owned there:

```csharp
// DCLWebSocket.CloseAsync owns the socket, so it owns the race
try
{
    await ws.CloseAsync(statusType, description, cancellationToken);
}
catch (System.Net.WebSockets.WebSocketException e) when (e.InnerException is ObjectDisposedException)
{
    // Mono surfaces the Dispose() race as a WebSocketException wrapping the ObjectDisposedException.
}
catch (System.Net.WebSockets.WebSocketException e)
{
    throw new WebSocketException(e);
}
```

With the race owned there, higher layers such as `ClientWebSocketApiImplementation.CloseAsync`
must **not** re-catch `ObjectDisposedException` (nor `e.InnerException is ObjectDisposedException`);
they only absorb `OperationCanceledException` for a close cancelled mid-flight.

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
