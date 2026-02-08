using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Global.Dynamic;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.RealmNavigation;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Systems;
using Global.AppArgs;
using Temp.Helper.WebClient;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace Global.Dynamic
{
    public class RealmController : IGlobalRealmController
    {
        // TODO it can be dangerous to clear the realm, instead we may destroy it fully and reconstruct but we will need to
        // TODO construct player/camera entities again and allocate more memory. Evaluate
        // Realms + Promises
        private static readonly QueryDescription CLEAR_QUERY = new QueryDescription().WithAny<RealmComponent, GetSceneDefinition, GetSceneDefinitionList, SceneDefinitionComponent, EmptySceneComponent>()
                                                                                     .WithNone<PortableExperienceComponent, SmartWearableId>();
        private static readonly QueryDescription CLEAR_UNFINISHED_QUERY = new QueryDescription()
                                                                         .WithAll<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, SceneLoadingState>()
                                                                         .WithNone<DeleteEntityIntention, ISceneFacade>();

        private static readonly QueryDescription INVALIDATE_PARTITIONS = new QueryDescription()
                                                                        .WithAll<PartitionComponent, ISceneFacade>()
                                                                        .WithNone<PortableExperienceComponent, SmartWearableId>();

        private readonly List<ISceneFacade> allScenes = new (PoolConstants.SCENES_COUNT);
        private readonly ServerAbout serverAbout = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IReadOnlyList<int2> staticLoadPositions;
        private readonly RealmData realmData;
        private readonly RetrieveSceneFromFixedRealm retrieveSceneFromFixedRealm;
        private readonly RetrieveSceneFromVolatileWorld retrieveSceneFromVolatileWorld;
        private readonly TeleportController teleportController;
        private readonly PartitionDataContainer partitionDataContainer;
        private readonly IScenesCache scenesCache;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly bool isLocalSceneDevelopment;
        private readonly RealmNavigatorDebugView realmNavigatorDebugView;
        private readonly URLDomain assetBundleRegistry;
        private readonly IAppArgs appArgs;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly DecentralandEnvironment environment;

        private GlobalWorld? globalWorld;
        private Entity realmEntity;

        public IRealmData RealmData => realmData;

        public URLDomain? CurrentDomain { get; private set; }

        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set
            {
                globalWorld = value;
                teleportController.World = globalWorld.EcsWorld;
            }
        }

        public RealmController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            TeleportController teleportController,
            RetrieveSceneFromFixedRealm retrieveSceneFromFixedRealm,
            RetrieveSceneFromVolatileWorld retrieveSceneFromVolatileWorld,
            IReadOnlyList<int2> staticLoadPositions,
            RealmData realmData,
            IScenesCache scenesCache,
            PartitionDataContainer partitionDataContainer,
            IComponentPool<PartitionComponent> partitionComponentPool,
            RealmNavigatorDebugView realmNavigatorDebugView,
            bool isLocalSceneDevelopment,
            URLDomain assetBundleRegistry,
            IAppArgs appArgs,
            IDecentralandUrlsSource decentralandUrlsSource,
            DecentralandEnvironment environment)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.staticLoadPositions = staticLoadPositions;
            this.realmData = realmData;
            this.teleportController = teleportController;
            this.retrieveSceneFromFixedRealm = retrieveSceneFromFixedRealm;
            this.retrieveSceneFromVolatileWorld = retrieveSceneFromVolatileWorld;
            this.scenesCache = scenesCache;
            this.partitionDataContainer = partitionDataContainer;
            this.partitionComponentPool = partitionComponentPool;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
            this.realmNavigatorDebugView = realmNavigatorDebugView;
            this.assetBundleRegistry = assetBundleRegistry;
            this.appArgs = appArgs;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.environment = environment;
        }

        public async UniTask SetRealmAsync(URLDomain realm, CancellationToken ct)
        {
            WebGLDebugLog.Log("RealmController.cs", "SetRealmAsync start", realm.ToString());
            World world = globalWorld!.EcsWorld;

            try { await UnloadCurrentRealmAsync(); }
            catch (ObjectDisposedException) { }
            catch (Exception e) { throw new RealmChangeException("Cannot unload current realm", e); }

            await UniTask.SwitchToMainThread();

            URLAddress url = realm.Append(new URLPath("/about"));
            bool isWorldRealm = IsWorldRealmUrl(realm);

            try
            {
                ServerAbout result;
                IIpfsRealm ipfsRealm;
                string realmName;
                int networkId;
                string hostname;

                if (isWorldRealm)
                {
                    try
                    {
                        // WebGL: JsonUtility can NRE in main-app bootstrap context; use Newtonsoft. Other platforms: keep Unity.
                        result = await webRequestController
                            .GetAsync(new CommonArguments(url), ct, ReportCategory.REALM)
#if UNITY_WEBGL && !UNITY_EDITOR
                            .CreateFromJson<ServerAbout>(WRJsonParser.Newtonsoft);
#else
                            .CreateFromJson<ServerAbout>(WRJsonParser.Unity);
#endif
                        if (result == null)
                            throw new RealmChangeException("Failed to parse world about (empty or invalid response).", new InvalidOperationException("CreateFromJson returned null"));
                        try { EnsureWorldRealmAboutFilled(realm, result); }
                        catch (Exception ex) { LogRealmStepError("1_EnsureWorldRealmAboutFilled", ex); throw; }
                        string worldName;
                        try
                        {
                            worldName = ExtractWorldNameFromRealm(realm);
                            ipfsRealm = new WorldIpfsRealm(worldName, result);
                            realmName = result.configurations?.realmName ?? worldName;
                            networkId = result.configurations?.networkId ?? 1;
                            hostname = $"worlds-content-server.decentraland.org/world/{worldName}";
                        }
                        catch (Exception ex) { LogRealmStepError("2_WorldIpfsRealm_and_names", ex); throw; }
                    }
                    catch (NullReferenceException nre)
                    {
                        throw new RealmChangeException("Failed to parse world about (empty or invalid response).", nre);
                    }
                }
                else
                {
                    GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);
                    result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);
                    EnsureWorldRealmAboutFilled(realm, result);
                    if (environment.Equals(DecentralandEnvironment.Today))
                    {
                        result.content.publicUrl = decentralandUrlsSource.Url(DecentralandUrl.DecentralandContentOverride);
                        result.lambdas.publicUrl = decentralandUrlsSource.Url(DecentralandUrl.DecentralandLambdasOverride);
                    }
                    hostname = ResolveHostname(realm, result);
                    ipfsRealm = new IpfsRealm(web3IdentityCache, webRequestController, realm, assetBundleRegistry, result);
                    realmName = result.configurations.realmName.EnsureNotNull("Realm name not found");
                    networkId = result.configurations.networkId;
                }

                WebGLDebugLog.Log("RealmController.cs", "SetRealmAsync /about fetched, about to Reconfigure", realmName ?? "(null)");
                try
                {
                    realmData.Reconfigure(
                        ipfsRealm,
                        realmName,
                        networkId,
                        ResolveCommsAdapter(result),
                        result.comms?.protocol ?? "v3",
                        hostname,
                        isLocalSceneDevelopment
                    );
                }
                catch (Exception ex) { LogRealmStepError("3_RealmData_Reconfigure", ex); throw; }

                WebGLDebugLog.Log("RealmController.cs", "SetRealmAsync Reconfigure done, realm configured", realmName ?? "(null)");
                RealmComponent realmComp;
                try
                {
                    realmComp = new RealmComponent(realmData);
                    realmEntity = world.Create(realmComp, ProcessedScenePointers.Create());
                }
                catch (Exception ex) { LogRealmStepError("4_RealmComponent_and_Create_entity", ex); throw; }

                try
                {
                    if (!ComplimentWithStaticPointers(world, realmEntity) && !realmComp.ScenesAreFixed)
                        ComplimentWithVolatilePointers(world, realmEntity);
                    IRetrieveScene sceneProviderStrategy = realmData.ScenesAreFixed ? retrieveSceneFromFixedRealm : retrieveSceneFromVolatileWorld;
                    sceneProviderStrategy.World = globalWorld.EcsWorld;
                    teleportController.SceneProviderStrategy = sceneProviderStrategy;
                    partitionDataContainer.Restart();
                    CurrentDomain = realm;
                    if (realmNavigatorDebugView.DebugWidgetBuilder != null)
                        realmNavigatorDebugView.UpdateRealmName(CurrentDomain.Value.ToString(), result.lambdas?.publicUrl ?? "", result.content?.publicUrl ?? "");
                }
                catch (Exception ex) { LogRealmStepError("5_ComplimentPointers_through_UpdateRealmName", ex); throw; }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                try
                {
                    string realmUrl = url.Value;
                    string msg = string.IsNullOrEmpty(realmUrl) ? "Failed to connect to realm: " + e.Message : $"Failed to connect to '{realmUrl}': {e.Message}";
                    Debug.LogError($"[Realm] {msg}");
                    WebGLDebugLog.LogError("RealmController.cs", $"SetRealmAsync failed: {e.GetType().Name}", $"{e.Message}\n{e.StackTrace}");
                }
                catch
                {
                    /* avoid secondary throw when building log message */
                }
                throw new RealmChangeException("Failed to connect to realm.", e);
            }
        }

        public async UniTask RestartRealmAsync(CancellationToken ct)
        {
            if (!CurrentDomain.HasValue)
                throw new Exception("Cannot restart realm, no valid domain set. First call SetRealmAsync(domain)");

            await SetRealmAsync(CurrentDomain.Value, ct);
        }

        public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            await webRequestController.IsHeadReachableAsync(ReportCategory.REALM, realm.Append(new URLPath("/about")), ct);

        public async UniTask<bool> IsUserAuthorisedToAccessWorldAsync(URLDomain realm, CancellationToken ct)
        {
            const string SIGN_METADATA = "{\"intent\": \"dcl:explorer:comms-handshake\",\"signer\":\"dcl:explorer\",\"isGuest\":false}";
            ServerAbout about = await webRequestController.GetAsync(new CommonArguments(realm.Append(new URLPath("/about"))), ct, ReportCategory.REALM).CreateFromJson<ServerAbout>(WRJsonParser.Unity);

            string commsAdapterUrl = ExtractCommsAdapterUrl(about.comms?.adapter ?? string.Empty);

            if (string.IsNullOrEmpty(commsAdapterUrl))
                return true;

            long statusCode;

            try
            {
                statusCode = await webRequestController.SignedFetchPostAsync(
                                                            commsAdapterUrl,
                                                            SIGN_METADATA,
                                                            ct)
                                                       .StatusCodeAsync();
            }
            catch (UnityWebRequestException e) { statusCode = e.ResponseCode; }

            return statusCode != 401;
        }

        public async UniTask<AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]> WaitForFixedScenePromisesAsync(CancellationToken ct)
        {
            FixedScenePointers fixedScenePointers = default;

            await UniTask.WaitUntil(() => GlobalWorld.EcsWorld.TryGet(realmEntity, out fixedScenePointers)
                                          && fixedScenePointers.AllPromisesResolved, cancellationToken: ct);

            return fixedScenePointers.Promises!;
        }

        public async UniTask<SceneDefinitions?> WaitForStaticScenesEntityDefinitionsAsync(CancellationToken ct)
        {
            if (staticLoadPositions.Count == 0) return null;

            World world = GlobalWorld.EcsWorld;

            var intention = new GetSceneDefinitionList(new List<SceneEntityDefinition>(staticLoadPositions.Count), staticLoadPositions, new CommonLoadingArguments(RealmData.Ipfs.EntitiesActiveEndpoint));
            var promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world, intention, PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (ct.IsCancellationRequested || !promise.TryGetResult(world, out var result) || !result.Succeeded) return null;

            var sceneDefinitions = result.Asset;
            if (world.TryGet(realmEntity, out SmartWearablePreviewScene smartWearablePreviewScene) && smartWearablePreviewScene.Value != Entity.Null)
            {
                // In local scene development we can be loading a Smart Wearable preview scene
                // In that case the scene definition cannot be found at the standard active entities endpoint
                // But, we can retrieve it from the Smart Wearable preview scene component that's already been created
                var sceneDefinitionComponent = world.Get<SceneDefinitionComponent>(smartWearablePreviewScene.Value);
                sceneDefinitions.Value.Add(sceneDefinitionComponent.Definition);
            }

            return sceneDefinitions;

        }

        public void DisposeGlobalWorld()
        {
            List<ISceneFacade> loadedScenes = allScenes;

            if (globalWorld != null)
            {
                RemoveUnfinishedScenes(globalWorld.EcsWorld);

                loadedScenes = FindLoadedScenesAndClearSceneCache(true);

                // Destroy everything without awaiting as it's Application Quit
                globalWorld.SafeDispose(ReportCategory.SCENE_LOADING);
            }

            foreach (ISceneFacade scene in loadedScenes)

                // Scene Info is contained in the ReportData, don't include it into the exception
                scene.SafeDispose(new ReportData(ReportCategory.SCENE_LOADING, sceneShortInfo: scene.Info),
                    static _ => "Scene's thrown an exception on Disposal: it could leak unpredictably");
        }

        private async UniTask UnloadCurrentRealmAsync()
        {
            //No need to dispose if we are quitting. Pools and assets may be destroyed by Unity, creating unnecessarily null-refs on exit
            if (UnityObjectUtils.IsQuitting)
                return;

            if (globalWorld == null) return;

            World world = globalWorld.EcsWorld;

            RemoveUnfinishedScenes(world);

            InvalidateScenePartitions(world);

            List<ISceneFacade> loadedScenes = FindLoadedScenesAndClearSceneCache();

            // release pooled entities
            for (var i = 0; i < globalWorld.FinalizeWorldSystems.Count; i++)
                globalWorld.FinalizeWorldSystems[i].FinalizeComponents(world.Query(in CLEAR_QUERY));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            globalWorld.Clear();

            teleportController.InvalidateRealm();
            realmData.Invalidate();

            await UniTask.WhenAll(loadedScenes.Select(s => s.DisposeAsync()));

            CurrentDomain = null;

            // Collect garbage, good moment to do it
            GC.Collect();
        }

        private void InvalidateScenePartitions(World world)
        {
            world.Query(in INVALIDATE_PARTITIONS,
                (ref PartitionComponent partitionComponent) => { partitionComponent.Bucket = byte.MaxValue; });
        }

        private void ComplimentWithVolatilePointers(World world, Entity realmEntity)
        {
            world.Add(realmEntity, VolatileScenePointers.Create(partitionComponentPool.Get()));
        }

        private bool ComplimentWithStaticPointers(World world, Entity realmEntity)
        {
            if (staticLoadPositions is { Count: > 0 })
            {
                // Static scene pointers don't replace the logic of fixed pointers loading but compliment it
                world.Add(realmEntity, new StaticScenePointers(staticLoadPositions));
                return true;
            }

            return false;
        }

        private void RemoveUnfinishedScenes(World world)
        {
            // See https://github.com/decentraland/unity-explorer/issues/4935
            // The scene load process it is disrupted due to internet issues remaining in an invalid state
            // We need to remove them and reload them, otherwise they will keep in an inconsistent state forever
            world.Query(CLEAR_UNFINISHED_QUERY,
                (Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref SceneLoadingState sceneLoadingState) =>
                {
                    if (promise is { IsConsumed: true, Result: { Succeeded: false } })
                    {
                        world.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
                        world.Add<DeleteEntityIntention>(entity);
                        sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
                        sceneLoadingState.PromiseCreated = false;
                    }
                });
        }

        private List<ISceneFacade> FindLoadedScenesAndClearSceneCache(bool findPortableExperiences = false)
        {
            allScenes.Clear();
            allScenes.AddRange(scenesCache.Scenes);
            if (findPortableExperiences) allScenes.AddRange(scenesCache.PortableExperiencesScenes);

            // Dispose all scenes
            scenesCache.ClearScenes(findPortableExperiences);

            return allScenes;
        }

        private static bool IsWorldRealmUrl(URLDomain realm) =>
            realm.Value.IndexOf("worlds-content-server.decentraland.org/world/", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Worlds /about from worlds-content-server can have null content, lambdas, or configurations.
        /// Fill defaults so IpfsRealm and Reconfigure don't NRE.
        /// </summary>
        private static void EnsureWorldRealmAboutFilled(URLDomain realm, ServerAbout result)
        {
            bool isWorldRealm = IsWorldRealmUrl(realm);

            if (result.configurations == null)
            {
                result.configurations = new ServerConfiguration
                {
                    networkId = 1,
                    realmName = isWorldRealm ? ExtractWorldNameFromRealm(realm) : string.Empty,
                    scenesUrn = new List<string>()
                };
            }
            else if (string.IsNullOrEmpty(result.configurations.realmName) && isWorldRealm)
                result.configurations.realmName = ExtractWorldNameFromRealm(realm);

            if (result.configurations.scenesUrn == null)
                result.configurations.scenesUrn = new List<string>();

            if (result.content == null)
                result.content = new ContentEndpoint(isWorldRealm ? "https://worlds-content-server.decentraland.org/contents/" : string.Empty);
            else if (string.IsNullOrEmpty(result.content.publicUrl) && isWorldRealm)
                result.content.publicUrl = "https://worlds-content-server.decentraland.org/contents/";

            if (result.lambdas == null)
                result.lambdas = new ContentEndpoint("https://peer.decentraland.org/lambdas/");
            else if (string.IsNullOrEmpty(result.lambdas.publicUrl))
                result.lambdas.publicUrl = "https://peer.decentraland.org/lambdas/";
        }

        private static string ExtractWorldNameFromRealm(URLDomain realm)
        {
            string path = new Uri(realm.Value).AbsolutePath.TrimEnd('/');
            int lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path.Substring(lastSlash + 1).ToLowerInvariant() : path.ToLowerInvariant();
        }

        private static void LogRealmStepError(string step, Exception ex)
        {
            Debug.LogError($"[Realm] NRE at step {step}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
        }

        private string ResolveHostname(URLDomain realm, ServerAbout about)
        {
            string hostname;

            if (about?.configurations != null
                && !string.IsNullOrEmpty(about.configurations.realmName)
                && about.configurations.realmName.IsEns())
                hostname = $"worlds-content-server.decentraland.org/world/{about.configurations.realmName.ToLower()}";
            else
                hostname = about?.comms == null

                    // Consider it as the "main" realm which shares the comms with many catalysts
                    // TODO: take in consideration the web3-network. If its sepolia then it should be .zone
                    ? "realm-provider.decentraland.org"
                    : new Uri(realm.Value).Host;

            return hostname;
        }

        private string ResolveCommsAdapter(ServerAbout about)
        {
            if (appArgs.TryGetValue(AppArgsFlags.COMMS_ADAPTER, out string? arg) && !string.IsNullOrEmpty(arg))
                return arg;

            //"offline property like in previous implementation"
            return about.comms?.adapter ?? about.comms?.fixedAdapter ?? "offline:offline";
        }

        private static string ExtractCommsAdapterUrl(string input)
        {
            const string MARKER = "https";
            int index = input.IndexOf(MARKER, StringComparison.InvariantCulture);
            return index >= 0 ? input.Substring(index) : string.Empty;
        }
    }
}
