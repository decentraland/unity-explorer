# Feature Flags

The client fetches the information configured at https://features.decentraland.systems/
This process occurs at the start of the application, before the plugins are initialized, so we ensure that we get the data as soon as possible.

## How we fetch the data

We make a call `IFeatureFlagsProvider.GetAsync(options, ct)`. This finally triggers an HTTP request like:
```bash
curl --location 'https://feature-flags.decentraland.org/explorer.json' \
--header 'X-Address-Hash: <anonymous user id>' \
--header 'X-Debug: false' \
--header 'referer: https://decentraland.org'
```

We require to set the `FeatureFlagOptions options` param:
- UserId: the identity the flags are evaluated against, sent as the `X-Address-Hash` header. Unleash treats it as an opaque string — it feeds gradual-rollout bucketing and the https://gh.getunleash.io/reference/activation-strategies#userids strategy, and does not have to be a wallet address. See [Which identity is sent](#which-identity-is-sent).
- URL: decentraland systems uses either https://feature-flags.decentraland.org or https://feature-flags.decentraland.zone
- AppName: refers to the application concept: https://docs.getunleash.io/reference/applications. `explorer` is set by default
- Hostname: Applies for application hostname strategy: https://gh.getunleash.io/reference/activation-strategies#hostnames. i.e.: decentraland.org, decentraland.zone, localhost

### Which identity is sent

The client sends an **anonymous user id**, never the wallet address. It is resolved in this order:

1. `--feature-flags-user-id`, an explicit override for QA — see
   [below](#how-to-change-options-through-program-args).
2. `--campaign_anon_user_id`, forwarded by the launcher from the website — the id the user is already known by at
   the original source of the funnel, so the explorer buckets them the same way the website did. The launcher
   passes it on every launch where it exists, so it needs no local copy.
3. `AnonymousInstallationId`, a `Guid` generated on the first launch that needs one and persisted under
   `DCLPrefKeys.ANONYMOUS_INSTALLATION_ID`.

Neither argument is ever written to prefs: the stored id is only the generated fallback.

That third id is not owned by feature flags. It is the installation's single anonymous identity, and the device
identifier sent to comms-gatekeeper is a hash of the same value (`AnonymousInstallationId.ResolveFingerprint`), so there is one
value to reason about and one to reset. Installations that predate the shared key keep the id they already had
under `FeatureFlagsUserId`, rather than being re-bucketed on upgrade.

The generated id is deliberately random rather than derived from `SystemInfo.deviceUniqueIdentifier`: the device id
is unavailable on some platforms (`SystemInfo.unsupportedIdentifier`), which would collapse every affected machine
into one rollout bucket, and it silently collides between cloned VM/VDI images and between machines whose firmware
reports placeholder serials. The comms fingerprint moved off it for that second reason; `GuestSessionIdProvider`
still hashes it behind a domain prefix (see issue #10199).

The trade-off is durability: clearing prefs or reinstalling yields a new id, so that install is re-bucketed. Note
also that concurrent instances each claim their own `userdata_{n}.json` slot and therefore resolve their own id.

Flags are fetched once during bootstrap, long before the user authenticates, and systems are built from that
snapshot. Bucketing on an id that already exists pre-login means a user lands in the same A/B group on their very
first session as on every later one, and the group never changes mid-session. Wallet-targeted allow-lists in this
client (`user-allow-list`, `alfa-official-wallets`, `banned_users`, ...) are delivered as CSV variant payloads and
matched client-side against the logged-in address, so they are unaffected by which identity the header carries.

One consequence: a flag configured server-side with the Unleash `userIds` strategy listing wallet addresses will not
match. Target such rollouts by percentage or by a client-side CSV allow-list instead.

## How to change options through program args

`--feature-flags-url`: represents the `options.URL` param to use different servers.

`--feature-flags-hostname`: represents the `options.Hostname` param as it is required to provide the configuration either for org, zone or local development.

`--feature-flags-user-id`: represents the `options.UserId` param, overriding the resolved anonymous id. Use it to evaluate the document as a specific user — to reproduce the flags a reporter sees, or to land in a particular A/B bucket. Like the two params above it is denied to `decentraland://` deep links by `DeepLinkAllowlist`, and it is never persisted.

An example if you want to set local development mode:

```bash
./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname localhost
```

Another example if you want to set it on zone (dev):
```bash
./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname https://decentraland.zone
```

## How to name feature flags

The flag names in https://features.decentraland.systems/ should use the `explorer-alfa` prefix, e.g., `explorer-alfa-your-feature`. This convention helps differentiate feature flags from the old client.

When received by the client, the `explorer` prefix will be removed, and feature flags will follow the format `alfa-your-feature`.

In the codebase, we should also name these flags using the `alfa` prefix, e.g., `alfa-your-feature`.

## How to check if a feature is enabled

The feature flag configuration is set into `FeatureFlagsCache`.

```csharp
private readonly FeatureFlagsCache featureFlagsCache;

public MyClass(FeatureFlagsCache featureFlagsCache)
{
    this.featureFlagsCache = featureFlagsCache;
}

public void DoStuff()
{
    if (!featureFlagsCache.Configuration.IsEnabled("any-feature")) return;
    // Do your feature stuff
}
```

From an automation test, read the flags out of the running client via
`AltTesterFeatureFlagsProbe` rather than fetching the remote document — see
[Static Probes](automation-testing.md#static-probes).

## How to get content of a feature flag (variants)

Refer to: https://gh.getunleash.io/reference/strategy-variants#what-are-strategy-variants


This is how you check if the variant is enabled:
```csharp
if (!featureFlagsCache.Configuration.IsEnabled("any-feature", "my-variant")) return;
```

You can get the content in three different formats depending on how it's configured: string, json or csv.

```csharp
private readonly FeatureFlagsCache featureFlagsCache;

public MyClass(FeatureFlagsCache featureFlagsCache)
{
    this.featureFlagsCache = featureFlagsCache;
}

public void DoStringStuff()
{
    if (!featureFlagsCache.Configuration.IsEnabled("any-feature", "string-variant")) return;
    if (!featureFlagsCache.Configuration.TryGetTextPayload("any-feature", "string-variant", out string? str))

    // Do your feature stuff
}

public void DoCsvStuff()
{
    if (!featureFlagsCache.Configuration.IsEnabled("any-feature", "csv-variant")) return;
    if (!featureFlagsCache.Configuration.TryGetCsvPayload("user-allow-list", "csv-variant", out List<List<string>>? csv)) return;

    foreach (string value in csv[0])
    {
        // ...
    }
}

[Serializable]
struct MyJsonDto
{
    public string foo;
    public int bar;
}

public void DoJsonStuff()
{
    if (!featureFlagsCache.Configuration.IsEnabled("any-feature", "json-variant")) return;
    if (!featureFlagsCache.Configuration.TryGetCsvPayload("user-allow-list", "json-variant", out MyJsonDto? json)) return;

    // Do your feature stuff
}
```

## Temporal loading screen tips

The FF `alfa-temporal-loading-screen-tip` has a `main` variant that allows to enable/disable some loading screens based on the current date/time.
A configuration example could be the following:
```json
{
    "displayed": [
        {
            "name": "Summer Sale",
            "startDate": "2024-06-01",
            "endDate": "2024-08-31"
        },
        {
            "name": "Help Center"
        },
        {
            "name": "Holiday Special",
            "startDate": "2024-12-01",
            "endDate": "2024-12-25T12:34"
        }
    ]
}
```
Notice that:

1. Not specifying the dates means "always active"
2. Dates only are valid (time will be 00:00:00)
3. Dates with time are also valid
4. Dates must be in ISO format and expressed in UTC
5. Both date bounds must be specified or the config won't be temporal

## Nearby Voice Chat intro tip

The FF `alfa-nearby-voice-chat-tip` gates the Nearby Voice Chat introductory tip independently from the Nearby Voice Chat feature itself, so the tip can be turned off remotely without a client release.

It is a **kill switch**: unlike most feature flags it has no editor or app-arg fallback, so the tip stays hidden until the flag is explicitly enabled. To exercise it locally, point the client at a server where the flag is on:

```bash
./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname https://decentraland.zone
```

Its optional `config` variant carries the display frequency:

```json
{
    "showEverySessions": 5,
    "maxTimesShown": 2
}
```

- `showEverySessions` — how many launches must pass since the previous display before the tip is due again. Defaults to `5`.
- `maxTimesShown` — how many times the tip may ever be displayed to a user. Defaults to `2`.

With the defaults a fresh user sees the tip on launch 5 and again on launch 10, then never again. Both fields are optional; a missing payload or a missing field falls back to the default.

The gap is measured from the **last display**, not from launch 0. A returning user who is already well past every threshold when the flag is enabled therefore gets one display on their next launch and the second one a full period later — they never burn both displays on consecutive launches.

Independently of the flag, the tip is never shown again to a user who has spoken over nearby voice chat, who pressed the tip's own "Try it now" button, or who dismissed the tip back when it was shown once on first login. Merely closing the tip does not retire it — that user still gets their remaining scheduled display.
