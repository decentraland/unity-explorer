# Web Requests Framework

## General Structure

We are using a custom generic structure for performing `Web Requests` in an asynchronous way. It's located in `DCL.WebRequests` assembly.

Currently, the exposed API looks like this:
```csharp
public interface IWebRequestController
{
    UniTask<GenericGetRequest> GetAsync(
        CommonArguments commonArguments,
        CancellationToken ct,
        string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
        WebRequestHeadersInfo? headersInfo = null,
        WebRequestSignInfo? signInfo = null);

    UniTask<GenericPostRequest> PostAsync(
        CommonArguments commonArguments,
        GenericPostArguments arguments,
        CancellationToken ct,
        string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
        WebRequestHeadersInfo? headersInfo = null,
        WebRequestSignInfo? signInfo = null);

    UniTask<GetTextureWebRequest> GetTextureAsync(
        CommonArguments commonArguments,
        GetTextureArguments args,
        CancellationToken ct,
        string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
        WebRequestHeadersInfo? headersInfo = null,
        WebRequestSignInfo? signInfo = null);
}
```

Every type of request creates a strongly typed structure to define and restrain the set of possible operations. E.g. to prevent reading `text` from `GetTexture` request. This capability is not provided by Unity itself: it will just throw an exception.

The whole API is designed to provide an allocation-free way of setting parameters:
- Everything is designed with `Value Types` unless it's restricted by Unity API itself
- `WebRequestHeadersInfo` uses the pool and is disposed of automatically when a `Web Request` fails or succeeds

## Common Arguments

```csharp
public readonly URLAddress URL;
public readonly int AttemptsCount = 3;
public readonly int Timeout = 0;
```

Repetitions are handled inside the implementation of `IWebRequestController`. If needed (for test purposes) this behavior can be overridden.

## Signing

Some requests require a signature to validate the identity of the caller. Those are sensitive APIs that should not be abused by third-party consumers.

The identity itself is provided by the `IWeb3Authenticator` implementation. Then with the capabilities of [AuthChain](https://adr.decentraland.org/adr/ADR-162) and [Nethereum Library](https://github.com/Nethereum/Nethereum) `x-identity-auth-chain-N` headers are set in accordance.

For this to work the following structure should be provided as an argument to the request:

```csharp
public readonly struct WebRequestSignInfo
{
    public readonly URLAddress SignUrl;

    public WebRequestSignInfo(URLAddress signUrl)
    {
        SignUrl = signUrl;
    }
}
```

## Common Response Parsing Scenarios

Our scenarios of handling responses are common, therefore, several utilities are presented to unify the approaches and get rid of potential boilerplate code.

- `GenericDownloadHandlerUtils` provides capabilities for generic `Post` and `Get` requests
   - `OverwriteFromJson<T>` - parse JSON into the existing object with `Unity`/`Newtonsoft` off/on the main thread
   - `CreateFromJson<T>` - create an object from JSON with `Unity`/`Newtonsoft` off/on the main thread
   - `GetDataCopy()` - produce a managed `byte[]`
   - Calling these methods will provide data and automatically release the underlying request
- Texture creation from `GetTextureWebRequest` (can be further expanded to include Sprites creation)

## Retry Policy

### Idempotency

Idempotency is the property of an operation that can be applied multiple times without changing the result beyond the initial execution.

By default, only `GET` requests are idempotent and, therefore, can be repeated without an extra explicit instruction. Those include not only generic `GET` requests but also assets such as `Textures` and `Audio`.

Additionally, in our environment, "Signed" requests are not idempotent as the signature may expire.

### Delay between retries

If a request is to be repeated, a backoff delay is applied:
- The initial default delay is 1 second
- With every unsuccessful attempt, it gets multiplied by the backoff multiplier, which is equal to `3` by default

### Default Policy

By default, a request can be repeated up to `2` times (1 original execution + 2 retries) under the following circumstances:
- If the network error is qualified for repetition and the request is idempotent
- If the DNS Lookup error occurred and the request is idempotent

### Error Codes Qualification

Code | Name | Retry? | What to Do | Notes
-- | -- | -- | -- | --
400 | Bad Request | No | Fix payload/params; validate before send. | Malformed JSON, missing fields.
401 | Unauthorized | No | Refresh token once, then retry. | If second fail -> force re-login. Can't be applied automatically
403 | Forbidden | No | Block UI path or request proper scope. | Auth OK, but not allowed.
404 | Not Found | No | Check URL/IDs; refresh local cache. | Common for stale resource IDs.
405 | Method Not Allowed | No | Use correct HTTP verb. | Server disallows current method.
406 | Not Acceptable | No | Change Accept header/format. | Unsupported response type.
408 | Request Timeout | Yes | Retry with backoff. | Server didn't receive body in time.
409 | Conflict | No | Resolve state/version conflict, then manual retry. | E.g., duplicate, version mismatch.
410 | Gone | No | Remove/redirect feature. | Resource intentionally removed.
411 | Length Required | No | Ensure Content-Length is set. | Usually auto by UnityWebRequest.
412 | Precondition Failed | No | Refresh ETag/version, retry once. | Optimistic concurrency failed.
413 | Payload Too Large | No | Chunk/compress upload. | Split files or use CDN.
414 | URI Too Long | No | Move params to body (POST). | Overlong query string.
415 | Unsupported Media Type | No | Fix Content-Type. | e.g., send JSON as application/json.
421 | Misdirected Request | No | Fix host/routing. | HTTP/2 specific.
422 | Unprocessable Entity | No | Fix semantic validation errors. | JSON OK syntactically, rules fail.
425 | Too Early | Yes | Wait briefly, retry once. | Rare; HTTP/2 early data.
428 | Precondition Required | No | Add conditional headers. | Similar to 412.
429 | Too Many Requests | Yes | "Retry-After" is respected and obligatory to be qualified for retrying | Rate limiting.
431 | Header Fields Too Large | No | Trim headers/cookies. | Reduce auth blobs.
451 | Unavailable For Legal Reasons | No | Inform user; disable feature. | Legal/geofence block.
500 | Internal Server Error | Yes | Backoff; maybe switch region. | Generic server crash/bug.
501 | Not Implemented | No | Downgrade client / change call. | Endpoint/method not supported.
502 | Bad Gateway | Yes | Backoff; transient proxy/CDN issue. | Upstream sent bad response.
503 | Service Unavailable | Yes | "Retry-After" is respected and obligatory to be qualified for retrying | Overload/maintenance.
504 | Gateway Timeout | Yes | Retry. | Upstream took too long.
505 | HTTP Version Not Supported | No | Use supported protocol. | Rare with Unity.
507 | Insufficient Storage | No | Can't make any assumptions | Server disk full.
508 | Loop Detected | No | Can't make any assumptions | WebDAV-specific.
511 | Network Auth Required | No | Prompt captive-portal login. | Intercepting proxy.
521/522/523/525/526 | CDN Origin Errors | Yes | Treat like 502/504; backoff. Can recover | Cloudflare/Akamai variants.

### Enforced Policy

By calling `RetryPolicy.Enforce()`, it's possible to qualify the request for retries even if it's not qualified by default rules.

### JS API Policy

The requests originating from `SimpleFetch` API are not qualified for repetitions unless "Retry-Delay" is specified in the `429` or `503` response.

### ECS Originated Requests

Streamable Assets are not repeated directly by the `WebRequestController` but use their own system with promises which obey budgeting and prioritization.

## Analytics

`IWebRequestsAnalyticsContainer` records currently ongoing requests. At the moment it's used just for counting and displaying the value in the Debug View via `ShowWebRequestsCountersSystem` but in the future can be expanded to hold additional information about requests.
