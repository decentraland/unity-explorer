using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Plugin;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Nametags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.ResourcesUnloading;
using DCL.Utilities;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using DCL.Input.Component;
using DCL.CharacterCamera.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.DebugUtilities.UIBindings;
using DCL.Input;
using DCL.Time.Systems;
using ECS;
using Global;
using Utility;

namespace SceneRuntime.WebClient.Bootstrapper
{
    /// <summary>
    ///     Minimal bootstrapper that loads a single in-world avatar (no scene, no JS).
    ///     Use in Editor or WebGL to test avatar shaders without the full game flow.
    /// </summary>
    public class WebGLAvatarBootstrapper : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [Tooltip("Settings for Character, CharacterCamera, Avatar (global plugins).")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [Tooltip("Settings for DefaultTexturesContainer (same as main app’s scene plugin container). If unset, global is used.")]
        [SerializeField] private PluginSettingsContainer? scenePluginSettingsContainer;

        private string? errorMessage;
        private bool hasError;
        private bool isInitialized;

        private Utility.ExposedTransform? exposedPlayerTransform;
        private ExposedCameraData? exposedCameraData;
        private CharacterContainer? characterContainer;
        private World? globalWorldEcs;
        private SystemGroupWorld? globalWorldSystems;
        private ObjectProxy<AvatarBase>? mainPlayerAvatarBaseProxy;

        private void Start()
        {
            InitializeAsync().Forget();
        }

        private void Update()
        {
            if (globalWorldSystems == null) return;
            try { UpdateGlobalWorldSystems(globalWorldSystems); }
            catch (Exception e) { Debug.LogError($"[WebGLAvatarBootstrapper] Global world update: {e.Message}\n{e.StackTrace}"); }
        }

        private static void UpdateGlobalWorldSystems(SystemGroupWorld worldSystems)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo? prop = typeof(SystemGroupWorld).GetProperty("SystemGroups", FLAGS);
            if (prop?.GetValue(worldSystems) is not IReadOnlyList<Arch.SystemGroups.SystemGroup> groups)
                return;
            for (var i = 0; i < groups.Count; i++)
                groups[i].Update();
        }

        private void OnDestroy()
        {
            globalWorldSystems?.Dispose();
            globalWorldSystems = null;
            globalWorldEcs?.Dispose();
            globalWorldEcs = null;
        }

        private void OnGUI()
        {
            if (hasError && !string.IsNullOrEmpty(errorMessage))
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height - 20), errorMessage);
            }
            else if (!isInitialized)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(10, 10, 200, 30), "Loading avatar...");
            }
        }

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                FeaturesRegistry.InitializeEmpty();

                // Use production DCL URLs so wearable resolution and asset bundle loading succeed (lambdas + ab-cdn).
                IRealmData realmData = new IRealmData.Fake(
                    new WebClientStubImplementations.ProductionDclIpfsRealm(),
                    false,
                    "avatar-test",
                    true,
                    1,
                    "",
                    "v3",
                    "peer.decentraland.org"
                );

                exposedPlayerTransform = new ExposedTransform
                {
                    Position = new CanBeDirty<Vector3>(Vector3.zero),
                    Rotation = new CanBeDirty<Quaternion>(Quaternion.identity),
                };
                exposedCameraData = new ExposedCameraData
                {
                    WorldPosition = new CanBeDirty<Vector3>(Vector3.zero),
                };

                IAssetsProvisioner assetsProvisioner = new AddressablesProvisioner();
                characterContainer = new CharacterContainer(assetsProvisioner, exposedCameraData, exposedPlayerTransform);

                if (globalPluginSettingsContainer == null)
                {
                    SetError("Assign Global Plugin Settings Container (Character, Camera, Avatar). Optionally assign Scene Plugin Settings Container for DefaultTexturesContainer (e.g. World Plugins Container.asset).");
                    return;
                }

                (_, bool charInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(characterContainer, destroyCancellationToken);
                if (!charInitSuccess)
                {
                    SetError("Failed to initialize CharacterContainer. Ensure CharacterObject settings are in globalPluginSettingsContainer.");
                    return;
                }

                mainPlayerAvatarBaseProxy = new ObjectProxy<AvatarBase>();

                globalWorldEcs = World.Create();
                Entity globalPlayerEntity = globalWorldEcs.Create();

                Profile profile = Profile.NewRandomProfile("avatar-test");
                globalWorldEcs.Add(globalPlayerEntity, profile);
                globalWorldEcs.Add(globalPlayerEntity, PartitionComponent.TOP_PRIORITY);
                characterContainer!.InitializePlayerEntity(globalWorldEcs, globalPlayerEntity);
                // So systems that query for InputMapComponent (e.g. ControlCinemachineVirtualCameraSystem) find an entity; we don't use InputPlugin.
                globalWorldEcs.Add(globalPlayerEntity, new InputMapComponent(InputMapComponent.Kind.NONE));

                Entity globalSkyboxEntity = globalWorldEcs.Create();
                globalWorldEcs.Create(new SceneShortInfo(Vector2Int.zero, "global"));

                var globalSceneStateProvider = new SceneStateProvider();
                globalSceneStateProvider.State.Set(SceneState.Running);

                var globalComponentsContainer = ComponentsContainer.Create();
                // AvatarPlugin (and AvatarShapeHandlerSystem) need Transform pool; in the main app TransformsPlugin adds it when the scene world is built — we have no scene, so add it here.
                globalComponentsContainer.ComponentPoolsRegistry.AddGameObjectPool<Transform>(
                    onRelease: t => { t.ResetLocalTRS(); t.gameObject.layer = 0; },
                    maxSize: 2048);
                var stubDebugBuilder = new WebClientStubImplementations.StubDebugContainerBuilder();
                var realmSamplingData = new RealmSamplingData();
                var stubAppArgs = new ApplicationParametersParser(false);

                var characterCameraPlugin = new CharacterCameraPlugin(
                    assetsProvisioner,
                    realmSamplingData,
                    exposedCameraData!,
                    stubDebugBuilder,
                    stubAppArgs);
                (_, bool cameraInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(characterCameraPlugin, destroyCancellationToken);
                if (!cameraInitSuccess)
                {
                    SetError("Failed to initialize CharacterCameraPlugin. Add CharacterCameraSettings to globalPluginSettingsContainer.");
                    return;
                }

                var stubSceneReadiness = new WebClientStubImplementations.StubSceneReadinessReportQueue();
                var stubLandscape = new WebClientStubImplementations.StubLandscape();
                var stubScenesCache = new WebClientStubImplementations.StubScenesCache();
                var characterMotionPlugin = new CharacterMotionPlugin(
                    characterContainer.CharacterObject,
                    stubDebugBuilder,
                    globalComponentsContainer.ComponentPoolsRegistry,
                    stubSceneReadiness,
                    stubLandscape,
                    stubScenesCache);
                (_, bool motionInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(characterMotionPlugin, destroyCancellationToken);
                if (!motionInitSuccess)
                {
                    SetError("Failed to initialize CharacterMotionPlugin. Add CharacterMotionSettings (with CharacterControllerSettings) to globalPluginSettingsContainer.");
                    return;
                }

                var stubProfiler = new WebClientStubImplementations.StubBudgetProfiler();
                var stubMemoryCap = new WebClientStubImplementations.StubSystemMemoryCap();
                var memoryThreshold = new Dictionary<MemoryUsageStatus, float>
                {
                    { MemoryUsageStatus.ABUNDANCE, 0.6f },
                    { MemoryUsageStatus.WARNING, 0.8f },
                    { MemoryUsageStatus.FULL, 0.9f },
                };
                var frameTimeBudget = new FrameTimeCapBudget(33f, stubProfiler, () => false);
                var memoryBudget = new MemoryBudget(stubMemoryCap, stubProfiler, memoryThreshold);
                var cacheCleaner = new CacheCleaner(new WebClientStubImplementations.StubReleasablePerformanceBudget(), null);

                IPluginSettingsContainer settingsForDefaultTextures = scenePluginSettingsContainer != null ? scenePluginSettingsContainer : globalPluginSettingsContainer;
                (var defaultTexturesContainer, bool texturesOk) = await DefaultTexturesContainer.CreateAsync(
                    settingsForDefaultTextures,
                    assetsProvisioner,
                    stubAppArgs,
                    destroyCancellationToken);
                if (!texturesOk || defaultTexturesContainer == null)
                {
                    SetError(
                        "Failed to initialize DefaultTexturesContainer. In the main app it uses the scene plugin container. " +
                        "Assign 'World Plugins Container' (Assets/DCL/PluginSystem/World/World Plugins Container.asset) to Scene Plugin Settings Container.");
                    return;
                }

                NametagsData nametagsData = ScriptableObject.CreateInstance<NametagsData>();
                nametagsData.showNameTags = false;

                var userBlockingCacheProxy = new ObjectProxy<IUserBlockingCache>();
                userBlockingCacheProxy.SetObject(new WebClientStubImplementations.StubUserBlockingCache());

                // Shared storage: AvatarPlugin and WearablePlugin must use the same IWearableStorage instance.
                var wearableStorage = new WearableStorage();
                var trimmedWearableStorage = new TrimmedWearableStorage();
                IWebRequestController webRequestController = CreateWebRequestController();
                URLDomain assetBundleRegistryURL = realmData.Ipfs.AssetBundleRegistry;

                var wearablePlugin = new WearablePlugin(
                    webRequestController,
                    realmData,
                    assetBundleRegistryURL,
                    cacheCleaner,
                    wearableStorage,
                    trimmedWearableStorage,
                    builderContentURL: "",
                    builderCollectionsPreview: false);

                var avatarPlugin = new AvatarPlugin(
                    globalComponentsContainer.ComponentPoolsRegistry,
                    assetsProvisioner,
                    frameTimeBudget,
                    memoryBudget,
                    new WebClientStubImplementations.StubRendererFeaturesCache(),
                    realmData,
                    mainPlayerAvatarBaseProxy!,
                    stubDebugBuilder,
                    cacheCleaner,
                    nametagsData,
                    defaultTexturesContainer.TextureArrayContainerFactory,
                    wearableStorage,
                    userBlockingCacheProxy,
                    includeBannedUsersFromScene: false);

                (_, bool avatarInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(avatarPlugin, destroyCancellationToken);
                if (!avatarInitSuccess)
                {
                    SetError("Failed to initialize AvatarPlugin. Add AvatarShapeSettings to globalPluginSettingsContainer.");
                    return;
                }

                var builder = new ArchSystemsWorldBuilder<World>(globalWorldEcs);
                builder.InjectCustomGroup(new SyncedPresentationSystemGroup(globalSceneStateProvider));
                builder.InjectCustomGroup(new SyncedPreRenderingSystemGroup(globalSceneStateProvider));
                UpdateTimeSystem.InjectToWorld(ref builder);
                UpdatePhysicsTickSystem.InjectToWorld(ref builder);

                var pluginArgs = new GlobalPluginArguments(globalPlayerEntity, globalSkyboxEntity);
                characterContainer.CreateGlobalPlugin().InjectToWorld(ref builder, pluginArgs);
                characterCameraPlugin.InjectToWorld(ref builder, pluginArgs);
                characterMotionPlugin.InjectToWorld(ref builder, pluginArgs);
                wearablePlugin.InjectToWorld(ref builder, pluginArgs);
                avatarPlugin.InjectToWorld(ref builder, pluginArgs);

                globalWorldSystems = builder.Finish();
                globalWorldSystems.Initialize();

                var inputBlock = new ECSInputBlock(globalWorldEcs);
                inputBlock.EnableAll(InputMapComponent.Kind.FREE_CAMERA, InputMapComponent.Kind.EMOTE_WHEEL);
                DCLInput.Instance.UI.Enable();

                if (exposedCameraData.CameraEntityProxy.Object != default && globalWorldEcs.Has<CameraComponent>(exposedCameraData.CameraEntityProxy.Object))
                    mainCamera = globalWorldEcs.Get<CameraComponent>(exposedCameraData.CameraEntityProxy.Object).Camera;

                isInitialized = true;

                const float avatarTimeoutSeconds = 30f;
                float elapsed = 0f;
                while (elapsed < avatarTimeoutSeconds && !mainPlayerAvatarBaseProxy!.Configured)
                {
                    await UniTask.Yield(destroyCancellationToken);
                    elapsed += Time.deltaTime;
                }

                if (!mainPlayerAvatarBaseProxy.Configured)
                    Debug.LogWarning("[WebGLAvatarBootstrapper] Avatar did not finish loading within timeout. Check realm content URLs and wearable resolution.");
            }
            catch (Exception e)
            {
                SetError($"Failed to initialize: {e.Message}\n{e.StackTrace}");
            }
        }

        private static IWebRequestController CreateWebRequestController()
        {
            const int TOTAL_BUDGET = int.MaxValue;
            return new WebRequestController(
                new WebClientStubImplementations.StubWebRequestsAnalyticsContainer(),
                new IWeb3IdentityCache.Default(),
                new RequestHub(new WebClientStubImplementations.StubDecentralandUrlsSource()),
                Option<ChromeDevtoolProtocolClient>.None,
                new WebRequestBudget(TOTAL_BUDGET, new ElementBinding<ulong>(TOTAL_BUDGET)));
        }

        private void SetError(string message)
        {
            hasError = true;
            errorMessage = message;
            Debug.LogError($"[WebGLAvatarBootstrapper] {message}");
            enabled = false;
        }
    }
}
