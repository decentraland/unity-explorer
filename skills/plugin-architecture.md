# Plugin & Dependency Architecture

## Activation

Use this skill when adding or modifying plugins, plugin settings, containers, assembly structure, or dependency injection patterns.

## Sources

- `docs/architecture-overview.md` — Plugin system, containers, dependency management
- `docs/directories-and-assemblies-structure.md` — Folder and assembly organization rules

---

## Plugin Types

### IDCLWorldPlugin — Scene-Scoped

Created per scene world. Receives world-specific dependencies and injects systems into that world.

**Lifecycle:**
1. `InitializeAsync(TSettings, CancellationToken)` — Load addressable assets, create pools
2. `InjectToWorld(ref ArchSystemsWorldBuilder, ...)` — Register systems into the world
3. `Dispose()` — Clean up resources

**Key parameters in `InjectToWorld`:**
- `ECSWorldInstanceSharedDependencies sharedDependencies` — scene state, partition, scene data
- `PersistentEntities persistentEntities` — long-lived entities
- `List<IFinalizeWorldSystem> finalizeWorldSystems` — register cleanup systems here
- `List<ISceneIsCurrentListener> sceneIsCurrentListeners` — register scene-current listeners here

### Code Example — World Plugin

From `LightSourcePlugin.cs`:

```csharp
public class LightSourcePlugin : IDCLWorldPlugin<LightSourcePlugin.LightSourcePluginSettings>
{
    private readonly IComponentPoolsRegistry poolsRegistry;
    private readonly IAssetsProvisioner assetsProvisioner;
    private readonly CacheCleaner cacheCleaner;
    private readonly ICharacterObject characterObject;
    private readonly Arch.Core.World globalWorld;
    private readonly bool hasDebugFlag;

    private LightSourceSettings? lightSourceSettings;
    private IComponentPool<Light>? lightPoolRegistry;

    public LightSourcePlugin(
        IComponentPoolsRegistry poolsRegistry,
        IAssetsProvisioner assetsProvisioner,
        CacheCleaner cacheCleaner,
        ICharacterObject characterObject,
        Arch.Core.World globalWorld,
        bool hasDebugFlag)
    {
        this.poolsRegistry = poolsRegistry;
        this.assetsProvisioner = assetsProvisioner;
        this.cacheCleaner = cacheCleaner;
        this.characterObject = characterObject;
        this.globalWorld = globalWorld;
        this.hasDebugFlag = hasDebugFlag;
    }

    public void Dispose() { }

    public void InjectToWorld(
        ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities,
        List<IFinalizeWorldSystem> finalizeWorldSystems,
        List<ISceneIsCurrentListener> sceneIsCurrentListeners)
    {
        // Always inject the dirty-flag reset system for SDK components
        ResetDirtyFlagSystem<PBLightSource>.InjectToWorld(ref builder);

        // Inject feature systems
        var lifecycleSystem = LightSourceLifecycleSystem.InjectToWorld(
            ref builder, sharedDependencies.SceneStateProvider, lightPoolRegistry);
        LightSourceApplyPropertiesSystem.InjectToWorld(
            ref builder, sharedDependencies.SceneData, sharedDependencies.ScenePartition, lightSourceSettings);

        // Conditional debug system
        if (hasDebugFlag)
            LightSourceDebugSystem.InjectToWorld(ref builder, globalWorld);

        // Register cleanup
        finalizeWorldSystems.Add(lifecycleSystem);
    }

    public async UniTask InitializeAsync(LightSourcePluginSettings settings, CancellationToken ct)
    {
        Light lightSourcePrefab = (await assetsProvisioner.ProvideMainAssetAsync(
            settings!.LightSourcePrefab, ct)).Value.GetComponent<Light>();
        lightSourceSettings = (await assetsProvisioner.ProvideMainAssetAsync(
            settings.LightSourceSettings, ct)).Value;

        // Create and register pool
        lightPoolRegistry = poolsRegistry.AddGameObjectPool(
            () => Object.Instantiate(lightSourcePrefab, Vector3.zero, quaternion.identity),
            onRelease: OnPoolRelease,
            onGet: OnPoolGet);
        cacheCleaner.Register(lightPoolRegistry);
    }

    [Serializable]
    public class LightSourcePluginSettings : IDCLPluginSettings
    {
        public AssetReferenceGameObject LightSourcePrefab;
        public AssetReferenceT<LightSourceSettings> LightSourceSettings;
    }
}
```

### IDCLGlobalPlugin — Application-Scoped

Created once for the application lifetime. Injects systems into the global world only.

**Lifecycle:** Same as world plugin but with `GlobalPluginArguments` instead of world-specific dependencies.

### Code Example — Global Plugin

From `AudioPlaybackPlugin.cs`:

```csharp
public class AudioPlaybackPlugin : IDCLGlobalPlugin<AudioPlaybackPlugin.PluginSettings>
{
    private ProvidedInstance<UIAudioPlaybackController> uiAudioPlaybackController;
    private ProvidedInstance<WorldAudioPlaybackController> worldAudioPlaybackController;
    private ProvidedAsset<LandscapeAudioSystemSettings> landscapeAudioSettings;

    // ... constructor with shared dependencies ...

    public void Dispose()
    {
        // Dispose all provisioned assets
        uiAudioPlaybackController.Dispose();
        worldAudioPlaybackController.Dispose();
        landscapeAudioSettings.Dispose();
    }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
        in GlobalPluginArguments arguments)
    {
        // Conditional injection
        if (enableLandscape)
            LandscapeAudioCullingSystem.InjectToWorld(ref builder, terrainGenerator,
                worldTerrainGenerator, landscapeAudioSettings.Value,
                worldAudioPlaybackController.Value, realmData);
    }

    public async UniTask InitializeAsync(PluginSettings settings, CancellationToken ct)
    {
        // Use ProvidedInstance for proper disposal tracking
        uiAudioPlaybackController = await assetsProvisioner.ProvideInstanceAsync(
            settings.UIAudioPlaybackControllerReference, ct: ct);
        worldAudioPlaybackController = await assetsProvisioner.ProvideInstanceAsync(
            settings.WorldAudioPlaybackControllerReference, ct: ct);
        landscapeAudioSettings = await assetsProvisioner.ProvideMainAssetAsync(
            settings.LandscapeAudioSettingsReference, ct: ct);
    }

    [Serializable]
    public class PluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public LandscapeAudioSettingsReference LandscapeAudioSettingsReference { get; private set; }
        // ... other asset references ...
    }
}
```

## Plugin Settings

- Settings classes implement `IDCLPluginSettings` and are `[Serializable]`
- Asset references use `AssetReferenceT<T>` or `AssetReferenceGameObject` (Addressables)
- Settings are stored in `PluginSettingsContainer` ScriptableObject
- `IAssetsProvisioner` loads addressable assets; returns `ProvidedAsset<T>` or `ProvidedInstance<T>` for disposal tracking

## Container Architecture

### StaticContainer

Created first. Produces common dependencies and world plugins.

- Creates `IComponentPoolsRegistry`, `CacheCleaner`, `IAssetsProvisioner`
- Instantiates world plugins (`IDCLWorldPlugin`) with their dependencies
- Provides `StaticSettings` (all plugin settings)

### DynamicWorldContainer

Created after StaticContainer. Holds global plugins and runtime state.

- Instantiates global plugins (`IDCLGlobalPlugin`)
- Creates `RealmController`, `GlobalWorldFactory`
- Manages scene lifecycle

### ComponentsContainer

Registers SDK component types for CRDT deserialization. Each SDK component must be registered here.

## Assembly Structure

- **One assembly per feature** by default at `Assets/DCL/<Feature>`
- Control exposure via `public` / `internal` access levels
- Use Assembly Definition References to merge cross-dependent features
- All unit tests connect to single `DCL.Tests` assembly
- `DCL.Plugins` is the only global assembly — can reference any assembly but should not be referenced (except by tests)
- Plugins produce systems without knowledge about unrelated assemblies

### Feature folder structure:

```
Assets/DCL/<Feature>/
├── Components/
├── Systems/
├── Tests/
│   └── EditMode/
├── <Feature>Plugin.cs
└── <Feature>.asmdef
```

### Test Assembly References (`.asmref`)

Test projects use `.asmref` files to reference the parent test assembly instead of creating their own `.asmdef`. The `.asmref` file is a simple JSON file:

```json
{ "reference": "DCL.EditMode.Tests" }
```
