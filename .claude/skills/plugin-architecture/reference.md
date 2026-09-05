# Plugin & Dependency Architecture — Detailed Reference

## World Plugin Example — LightSourcePlugin

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

> **Note:** `LightSourcePlugin.cs` actually lives in `PluginSystem/World/` because it predates the current convention. New SDK component plugins should follow the `SDKComponents/<Feature>/Systems/` pattern described in the **sdk-component-implementation** skill.

---

## Global Plugin Example — AudioPlaybackPlugin

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
