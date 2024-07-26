using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Optimization.Pools;
using DCL.ParcelsService;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;

namespace Global.Dynamic
{
    public class RealmController : IGlobalRealmController
    {
        // TODO it can be dangerous to clear the realm, instead we may destroy it fully and reconstruct but we will need to
        // TODO construct player/camera entities again and allocate more memory. Evaluate
        // Realms + Promises
        private static readonly QueryDescription CLEAR_QUERY = new QueryDescription().WithAny<RealmComponent, GetSceneDefinition, GetSceneDefinitionList, SceneDefinitionComponent>();

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
        private readonly SceneAssetLock sceneAssetLock;

        private GlobalWorld? globalWorld;
        private Entity realmEntity;
        private URLDomain? currentDomain;

        public IRealmData RealmData => realmData;

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
            SceneAssetLock sceneAssetLock)
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
            this.sceneAssetLock = sceneAssetLock;
        }

        public async UniTask SetRealmAsync(URLDomain realm, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            try { await UnloadCurrentRealmAsync(); }
            catch (ObjectDisposedException) { }

            await UniTask.SwitchToMainThread();

            URLAddress url = realm.Append(new URLPath("/about"));

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);
            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, realm, result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty,
                result.comms?.protocol ?? string.Empty
            );

            // Add the realm component
            var realmComp = new RealmComponent(realmData);

            realmEntity = world.Create(realmComp, ProcessedScenePointers.Create());

            if (!ComplimentWithStaticPointers(world, realmEntity) && !realmComp.ScenesAreFixed)
                ComplimentWithVolatilePointers(world, realmEntity);

            IRetrieveScene sceneProviderStrategy = realmData.ScenesAreFixed ? retrieveSceneFromFixedRealm : retrieveSceneFromVolatileWorld;
            sceneProviderStrategy.World = globalWorld.EcsWorld;

            teleportController.SceneProviderStrategy = sceneProviderStrategy;
            partitionDataContainer.Restart();

            currentDomain = realm;
        }

        public async UniTask RestartRealmAsync(CancellationToken ct)
        {
            if (!currentDomain.HasValue)
                throw new Exception("Cannot restart realm, no valid domain set. First call SetRealmAsync(domain)");

            await SetRealmAsync(currentDomain.Value, ct);
        }

        public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            await webRequestController.IsReachableAsync(realm.Append(new URLPath("/about")), ct);

        public async UniTask<AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]> WaitForFixedScenePromisesAsync(CancellationToken ct)
        {
            FixedScenePointers fixedScenePointers = default;

            await UniTask.WaitUntil(() => GlobalWorld.EcsWorld.TryGet(realmEntity, out fixedScenePointers)
                                          && fixedScenePointers.AllPromisesResolved, cancellationToken: ct);

            return fixedScenePointers.Promises!;
        }

        public void DisposeGlobalWorld()
        {
            if (globalWorld != null)
            {
                World world = globalWorld.EcsWorld;
                FindLoadedScenes();
                world.Query(new QueryDescription().WithAll<SceneLODInfo>(), (ref SceneLODInfo lod) => lod.Dispose(world));

                // Destroy everything without awaiting as it's Application Quit
                globalWorld.SafeDispose(ReportCategory.SCENE_LOADING);
            }

            foreach (ISceneFacade scene in allScenes)

                // Scene Info is contained in the ReportData, don't include it into the exception
                scene.SafeDispose(new ReportData(ReportCategory.SCENE_LOADING, sceneShortInfo: scene.Info),
                    static _ => "Scene's thrown an exception on Disposal: it could leak unpredictably");
        }

        private async UniTask UnloadCurrentRealmAsync()
        {
            if (globalWorld == null) return;

            World world = globalWorld.EcsWorld;

            FindLoadedScenes();

            // release pooled entities
            for (var i = 0; i < globalWorld.FinalizeWorldSystems.Count; i++)
                globalWorld.FinalizeWorldSystems[i].FinalizeComponents(world.Query(in CLEAR_QUERY));

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(), (ref SceneLODInfo lod) => lod.Dispose(world));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            globalWorld.Clear();

            teleportController.InvalidateRealm();
            realmData.Invalidate();

            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));
            sceneAssetLock.Reset();

            currentDomain = null;

            // Collect garbage, good moment to do it
            GC.Collect();
        }

        private void ComplimentWithVolatilePointers(World world, Entity realmEntity)
        {
            world.Add(realmEntity, VolatileScenePointers.Create());
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

        private void FindLoadedScenes(bool findPortableExperiences = false)
        {
            allScenes.Clear();
            allScenes.AddRange(scenesCache.Scenes);
            if (findPortableExperiences) allScenes.AddRange(scenesCache.PortableExperiencesScenes);

            // Dispose all scenes
            scenesCache.ClearScenes(findPortableExperiences);
        }
    }
}
