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
        private readonly TeleportController teleportController;
        private readonly PartitionDataContainer partitionDataContainer;
        private readonly IScenesCache scenesCache;

        private GlobalWorld? globalWorld;
        public Dictionary<string,Entity> PortableExperienceEntities { get; } = new (); //Probably should be a dictionary using the url as key?
        private RealmData realmData = new RealmData();

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
            this.teleportController = teleportController;
            this.scenesCache = scenesCache;
            this.partitionDataContainer = partitionDataContainer;
        }

        public async UniTask CreatePortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            await UniTask.SwitchToMainThread();

            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            try {
                ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);
                if (result.configurations.scenesUrn.Count == 0)
                {
                    //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                    return;
                }

                realmData.Reconfigure(
                    new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                    result.configurations.realmName.EnsureNotNull("Realm name not found"),
                    result.configurations.networkId,
                    result.comms?.adapter ?? string.Empty
                );

                PortableExperienceEntities.Add(portableExperiencePath.Value, world.Create(new PortableExperienceComponent(realmData)));
            }
            catch (Exception e)
            {
                //Handle exception properly
                Console.WriteLine(e);
                throw;
            }

        }

        public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            await webRequestController.IsReachableAsync(realm.Append(new URLPath("/about")), ct);

        public IRealmData GetRealm() =>
            realmData;

        public async UniTask UnloadPortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct)
        {
            if (globalWorld == null) return;

            World world = globalWorld.EcsWorld;

            //ScenesCache.TryGetByURL

            // release pooled entities
            for (var i = 0; i < globalWorld.FinalizeWorldSystems.Count; i++)
                globalWorld.FinalizeWorldSystems[i].FinalizeComponents(world.Query(in CLEAR_QUERY));

            world.Query(new QueryDescription().WithAll<SceneLODInfo>(), (ref SceneLODInfo lod) => lod.Dispose(world));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            globalWorld.Clear();

            realmData.Invalidate();

            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));

            // Collect garbage, good moment to do it
            GC.Collect();
        }
    }
}
