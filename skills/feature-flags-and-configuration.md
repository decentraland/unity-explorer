# Feature Flags & Configuration

## Activation

Use this skill when gating features behind flags, checking runtime configuration, using the features registry, or working with app arguments.

## Sources

- `docs/feature-flags.md` — Runtime feature flag system
- `docs/features-registry.md` — Centralized feature state management
- `docs/app-arguments.md` — Command-line flags and arguments

---

## Feature Flags (Remote)

### Fetching

`IFeatureFlagsProvider.GetAsync()` calls the remote endpoint with:
- User address
- Debug flag
- Referer header

Default endpoint: `https://feature-flags.decentraland.org/explorer.json`

### Naming Convention

- **Server side:** `explorer-alfa-your-feature`
- **Codebase:** `alfa-your-feature` (prefix `explorer-` is stripped automatically)

### Checking Feature Flags

```csharp
// Simple check
bool enabled = featureFlagsCache.Configuration.IsEnabled("alfa-your-feature");

// Check with variant
bool enabled = featureFlagsCache.Configuration.IsEnabled("alfa-your-feature", "my-variant");

// Static shorthand (common pattern)
bool enabled = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS);
```

### Code Example — Feature Flag Gating

From `MapPinLoaderSystem.cs`:

```csharp
public partial class MapPinLoaderSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    private readonly bool useCustomMapPinIcons;

    public MapPinLoaderSystem(World world, /* ... */) : base(world)
    {
        // Cache flag value in constructor — checked every frame in Update
        useCustomMapPinIcons = FeatureFlagsConfiguration.Instance.IsEnabled(
            FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS);
    }

    protected override void Update(float t)
    {
        LoadMapPinQuery(World);
        UpdateMapPinQuery(World);
        HandleComponentRemovalQuery(World);
        HandleEntityDestructionQuery(World);

        // Conditionally run texture loading
        if (useCustomMapPinIcons)
            ResolveTexturePromiseQuery(World);
    }
}
```

### Variants

Feature flags can carry payload data in three formats:

```csharp
// String payload
if (config.TryGetTextPayload("alfa-feature", out string text))
    UseText(text);

// CSV payload
if (config.TryGetCsvPayload("alfa-feature", out string[] values))
    UseValues(values);

// JSON payload (deserializes to DTO type)
if (config.TryGetCsvPayload<MyConfigDto>("alfa-feature", out MyConfigDto dto))
    UseConfig(dto);
```

### Configuration Options

```csharp
var options = new FeatureFlagOptions
{
    UserId = userAddress,
    URL = flagsEndpoint,
    AppName = "explorer",
    Hostname = hostname
};
```

Overridable via app arguments:
- `--feature-flags-url` — Custom endpoint URL
- `--feature-flags-hostname` — Custom hostname

## Features Registry (Local)

`FeaturesRegistry` is a singleton that consolidates feature enable/disable logic from multiple sources: feature flags, app arguments, editor mode, and runtime conditions.

### Declaring Features

```csharp
// Each feature gets a FeatureId (numbered enum)
public enum FeatureId
{
    YOUR_FEATURE = 42,
}
```

### Registering Feature States

```csharp
// In SetFeatureStates (bulk registration)
public void SetFeatureStates(FeatureFlagsCache cache, AppArgs args)
{
    SetFeatureState(FeatureId.YOUR_FEATURE,
        cache.Configuration.IsEnabled("alfa-your-feature") || args.HasFlag("--enable-feature"));
}

// Or individually
FeaturesRegistry.Instance.SetFeatureState(FeatureId.YOUR_FEATURE, true);
```

### Checking Features

```csharp
bool enabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.YOUR_FEATURE);
```

### Feature Providers

For runtime-dependent features (e.g., user-specific checks):

```csharp
public class MyFeatureProvider : IFeatureProvider
{
    public FeatureId FeatureId => FeatureId.MY_FEATURE;

    public async UniTask<bool> IsFeatureEnabledAsync(CancellationToken ct)
    {
        // Runtime check (e.g., server call)
        return await CheckIfEnabledAsync(ct);
    }
}

// Register
FeaturesRegistry.Instance.RegisterFeatureProvider(new MyFeatureProvider());
```

Feature providers should cache values and reset on condition change.

## App Arguments

Command-line flags that configure application behavior. Work via command line or deep links.

### Usage

```
# Command line
--flag-name value

# Deep link
decentraland://?flag-name=value
```

### Key Flags by Category

| Category | Flag | Purpose |
|----------|------|---------|
| General | `--debug` | Enable debug mode |
| General | `--hub` | Hub mode |
| Environment | `--realm` | Target realm |
| Environment | `--local-scene` | Local scene development |
| Environment | `--position` | Starting position |
| Auth | `--skip-auth-screen` | Skip authentication |
| Performance | `--disable-disk-cache` | Disable disk cache |
| Performance | `--simulateMemory` | Simulate memory pressure |
| Development | `--use-log-matrix` | Custom log matrix JSON file |
| Development | `--launch-cdp-monitor-on-start` | Launch CDP monitor |
| Feature Flags | `--feature-flags-url` | Custom FF endpoint |
| Feature Flags | `--feature-flags-hostname` | Custom FF hostname |
| Display | `--windowed-mode` | Force windowed mode |

### Accessing in Code

```csharp
// Check flag presence
if (appArgs.HasFlag(AppArgsFlags.DEBUG))
    EnableDebugMode();

// Get flag value
string realm = appArgs.GetValue(AppArgsFlags.REALM);
```
