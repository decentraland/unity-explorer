# Feature Flags

The client fetches the information configured at https://features.decentraland.systems/
This process occurs at the start of the application, before the plugins are initialized, so we ensure that we get the data as soon as possible.

## How we fetch the data

We make a call `IFeatureFlagsProvider.GetAsync(options, ct)`. This finally triggers an HTTP request like:
```bash
curl --location 'https://feature-flags.decentraland.org/explorer.json' \
--header 'X-Address-Hash: 0x..usrAddr' \
--header 'X-Debug: false' \
--header 'referer: https://decentraland.org'
```

We require to set the `FeatureFlagOptions options` param:
- UserId: the user that is requesting the data. This is required by flags configured with strategy https://gh.getunleash.io/reference/activation-strategies#userids
- URL: decentraland systems uses either https://feature-flags.decentraland.org or https://feature-flags.decentraland.zone
- AppName: refers to the application concept: https://docs.getunleash.io/reference/applications. `explorer` is set by default
- Hostname: Applies for application hostname strategy: https://gh.getunleash.io/reference/activation-strategies#hostnames. i.e.: decentraland.org, decentraland.zone, localhost

### How to change options through program args

`--feature-flags-url`: represents the `options.URL` param to use different servers.

`--feature-flags-hostname`: represents the `options.Hostname` param as it is required to provide the configuration either for org, zone or local development.

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
