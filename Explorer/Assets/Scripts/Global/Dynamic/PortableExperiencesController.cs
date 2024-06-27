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
using ECS.SceneLifeCycle.Reporting;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    public class PortableExperiencesController : IPortableExperiencesController
    {
        // TODO it can be dangerous to clear the realm, instead we may destroy it fully and reconstruct but we will need to
        // TODO construct player/camera entities again and allocate more memory. Evaluate
        // Realms + Promises
        private static readonly QueryDescription CLEAR_QUERY = new QueryDescription().WithAny<RealmComponent, GetSceneDefinition, GetSceneDefinitionList, SceneDefinitionComponent>();

        private readonly List<ISceneFacade> allScenes = new (PoolConstants.SCENES_COUNT);
        private readonly ServerAbout serverAbout = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly RealmData realmData;
        private readonly TeleportController teleportController;
        private readonly PartitionDataContainer partitionDataContainer;
        private readonly IScenesCache scenesCache;

        private GlobalWorld? globalWorld;
        public List<Entity> RealmEntities { get; } = new (); //Probably should be a dictionary using the url as key?

        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set
            {
                globalWorld = value;
                teleportController.World = globalWorld.EcsWorld;
            }
        }

        public PortableExperiencesController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            TeleportController teleportController,
            IReadOnlyList<int2> staticLoadPositions,
            RealmData realmData,
            IScenesCache scenesCache,
            PartitionDataContainer partitionDataContainer)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.teleportController = teleportController;
            this.scenesCache = scenesCache;
            this.partitionDataContainer = partitionDataContainer;
        }

        public async UniTask CreatePortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            try { await UnloadCurrentRealmAsync(); }
            catch (ObjectDisposedException) { }

            await UniTask.SwitchToMainThread();

            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);
            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty
            );

            RealmEntities.Add(world.Create(new PortableExperienceComponent(realmData)));
        }

        public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            await webRequestController.IsReachableAsync(realm.Append(new URLPath("/about")), ct);

        public IRealmData GetRealm() =>
            realmData;

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

                // Destroy everything without awaiting as it's Application Quit
                globalWorld.SafeDispose(ReportCategory.SCENE_LOADING);
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
