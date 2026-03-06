# Web Requests

## Activation

Use this skill when making HTTP requests, parsing responses, signing requests with Web3 authentication, or configuring retry policies.

## Sources

- `docs/web-requests-framework.md` — Custom web request framework
- `docs/web3-authentication.md` — Web3 authentication, identity, and signing

---

## IWebRequestController API

The primary interface for all HTTP operations:

```csharp
// GET — strongly typed response
var response = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION);

// POST
var response = await webRequestController.PostAsync(commonArguments, body, ct, ReportCategory.MY_CATEGORY);

// GET texture
var response = await webRequestController.GetTextureAsync(commonArguments, ct, ReportCategory.TEXTURES);
```

## CommonArguments

```csharp
var commonArguments = new CommonArguments(
    url,                    // URLAddress — required
    attemptsCount: 3,       // Default: 3 (1 initial + 2 retries)
    timeout: 0              // Default: 0 (no timeout)
);
```

## Response Parsing

Use `GenericDownloadHandlerUtils` for response deserialization:

```csharp
// Overwrite existing object from JSON (avoids allocation)
await response.OverwriteFromJson(existingObject);

// Create new object from JSON
MyDto dto = await response.CreateFromJson<MyDto>();

// Create from Newtonsoft JSON (for complex types)
MyDto dto = await response.CreateFromNewtonsoftJsonAsync<MyDto>();

// Get raw bytes
byte[] data = await response.GetDataCopy();
```

### Code Example — URL Building + Request + Deserialization

From `TokenFileAuthenticator.cs`:

```csharp
private async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
{
    string token = contentResult.Value;

    // Build URL with URLBuilder
    urlBuilder.Clear();
    urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
              .AppendPath(new URLPath($"identities/{token}"));

    var commonArguments = new CommonArguments(urlBuilder.Build());

    // GET request with Newtonsoft JSON deserialization
    IdentityAuthResponseDto json = await webRequestController
        .GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
        .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>();

    // Use response data
    var authChain = AuthChain.Create();
    foreach (AuthLink authLink in json.identity.authChain)
        authChain.Set(authLink);
    // ...
}
```

## Request Signing

For authenticated requests, use `WebRequestSignInfo`:

```csharp
// Create sign info with IWeb3Authenticator
var signInfo = new WebRequestSignInfo(signUrl);

// The framework adds x-identity-auth-chain-N headers automatically
var response = await webRequestController.GetAsync(commonArguments, ct, reportCategory, signInfo);
```

### Auth Chain Construction

```csharp
// Sign a payload
AuthChain authChain = identity.Sign("payload");

// Manually set headers (if not using framework signing)
int i = 0;
foreach (AuthLink link in authChain)
{
    request.SetRequestHeader($"x-identity-auth-chain-{i}", link.ToJson());
    i++;
}
```

### Identity Access

```csharp
// Get user address
string address = web3IdentityCache.Identity.Address;

// Check if authenticated
if (web3IdentityCache.Identity != null) { /* ... */ }
```

## Retry Policy

### Default Behavior

- Only GET requests are idempotent by default
- Signed requests are NOT idempotent (won't retry)
- Default: up to 2 retries with exponential backoff (1s base, 3x multiplier)

### Error Code Qualification

| Code | Retry? | Reason |
|------|--------|--------|
| 400 | No | Bad Request |
| 401 | No | Unauthorized |
| 403 | No | Forbidden |
| 404 | No | Not Found |
| 408 | Yes | Request Timeout |
| 429 | Yes | Too Many Requests |
| 500 | Yes | Internal Server Error |
| 502 | Yes | Bad Gateway |
| 503 | Yes | Service Unavailable |
| 504 | Yes | Gateway Timeout |
| 511 | No | Network Auth Required |

### Forcing Retries

```csharp
// Force retry even for non-idempotent requests
RetryPolicy.Enforce()
```

### JS API

No retries unless `Retry-Delay` header present on 429/503.

## URL Utilities

Use typed URL structures from `URLHelpers`:

```csharp
// Domain
var domain = URLDomain.FromString("https://api.example.com");

// Address
var address = URLAddress.FromString("https://api.example.com/v1/users");

// Builder — chain construction
var builder = new URLBuilder();
builder.Clear();
builder.AppendDomain(domain)
       .AppendPath(new URLPath("v1/users"))
       .AppendParameter(new URLParameter("page", "1"));
URLAddress url = builder.Build();
```

## Design Notes

- The framework is designed to be **allocation-free** — value types throughout
- `WebRequestHeadersInfo` uses pooling for header storage
- `IWebRequestsAnalyticsContainer` tracks ongoing requests for analytics
