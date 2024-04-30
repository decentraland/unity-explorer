using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.ParcelsService;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.LOD.Components;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.WebRequests.GenericHead;
using ECS.SceneLifeCycle.Reporting;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    public class RealmController : IRealmController
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
        private readonly IScenesCache scenesCache;

        private GlobalWorld? globalWorld;
        public Entity RealmEntity { get; private set; }

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
            IScenesCache scenesCache)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.staticLoadPositions = staticLoadPositions;
            this.realmData = realmData;
            this.teleportController = teleportController;
            this.retrieveSceneFromFixedRealm = retrieveSceneFromFixedRealm;
            this.retrieveSceneFromVolatileWorld = retrieveSceneFromVolatileWorld;
            this.scenesCache = scenesCache;
        }

        /// <summary>
        ///     it is an async process so it should be executed before ECS kicks in
        /// </summary>
        public async UniTask SetRealmAsync(URLDomain realm, Vector2Int playerStartPosition, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await SetRealmAsync(realm, ct);

            loadReport.ProgressCounter.Value = 0.1f;

            var sceneLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            try
            {
                await UniTask.WhenAll(sceneLoadReport.PropagateAsync(loadReport, ct, loadReport.ProgressCounter.Value, timeout: TimeSpan.FromSeconds(30)),
                    teleportController.TeleportToSceneSpawnPointAsync(playerStartPosition, sceneLoadReport, ct).ContinueWith(w => w.ToUniTask(ct)));
            }
            catch (Exception e) { loadReport.CompletionSource.TrySetException(e); }
        }

        public async UniTask SetRealmAsync(URLDomain realm, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            try { await UnloadCurrentRealmAsync(); }
            catch (ObjectDisposedException) { }

            await UniTask.SwitchToMainThread();

            URLAddress url = realm.Append(new URLPath("/about"));

            var genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);
            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, realm, result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty
            );

            // Add the realm component
            var realmComp = new RealmComponent(realmData);

            RealmEntity = world.Create(realmComp, ProcessesScenePointers.Create());

            if (!ComplimentWithStaticPointers(world, RealmEntity) && !realmComp.ScenesAreFixed)
                ComplimentWithVolatilePointers(world, RealmEntity);

            IRetrieveScene sceneProviderStrategy = realmData.ScenesAreFixed ? retrieveSceneFromFixedRealm : retrieveSceneFromVolatileWorld;
            sceneProviderStrategy.World = globalWorld.EcsWorld;

            teleportController.SceneProviderStrategy = sceneProviderStrategy;
        }

        public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            var isReachable = true;

            URLAddress url = realm.Append(new URLPath("/about"));

            try
            {
                await webRequestController.HeadAsync(new CommonArguments(url), new WebRequestUtils.NoOp<GenericHeadRequest>(), default(GenericHeadArguments), ct);
            }
            catch (Exception)
            {
                isReachable = false;
            }

            return isReachable;
        }

        public IRealmData GetRealm() =>
            realmData;

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

        public async UniTask UnloadCurrentRealmAsync()
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

            // Collect garbage, good moment to do it
            GC.Collect();
        }

        public void DisposeGlobalWorld()
        {
            if (globalWorld != null)
            {
                World world = globalWorld.EcsWorld;
                FindLoadedScenes();
                world.Query(new QueryDescription().WithAll<SceneLODInfo>(), (ref SceneLODInfo lod) => lod.Dispose(world));
                world.Query(new QueryDescription().WithAll<ProcessesScenePointers>(), (ref ProcessesScenePointers scenePointers) => scenePointers.Value.Dispose());

                // Destroy everything without awaiting as it's Application Quit
                globalWorld.Dispose();
            }

            foreach (ISceneFacade scene in allScenes)

                // Scene Info is contained in the ReportData, don't include it into the exception
                scene.SafeDispose(new ReportData(ReportCategory.SCENE_LOADING, sceneShortInfo: scene.Info),
                    static _ => "Scene's thrown an exception on Disposal: it could leak unpredictably");
        }

        private void FindLoadedScenes()
        {
            allScenes.Clear();
            allScenes.AddRange(scenesCache.Scenes);

            // Dispose all scenes
            scenesCache.Clear();
        }
    }
}
