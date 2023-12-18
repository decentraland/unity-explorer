using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.ParcelsService;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Ipfs;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;

namespace Global.Dynamic
{
    public class RealmController : IRealmController
    {
        private static readonly QueryDescription SCENES = new QueryDescription().WithAll<ISceneFacade>();

        // TODO it can be dangerous to clear the realm, instead we may destroy it fully and reconstruct but we will need to
        // TODO construct player/camera entities again and allocate more memory. Evaluate
        // Realms + Promises
        private static readonly QueryDescription CLEAR_QUERY = new QueryDescription().WithAny<RealmComponent, GetSceneDefinition, GetSceneDefinitionList, SceneDefinitionComponent>();

        private readonly List<ISceneFacade> allScenes = new (PoolConstants.SCENES_COUNT);

        private readonly IpfsTypes.ServerAbout serverAbout = new ();

        private readonly IWebRequestController webRequestController;
        private readonly int sceneLoadRadius;
        private readonly IReadOnlyList<int2> staticLoadPositions;
        private readonly RealmData realmData;

        private readonly RetrieveSceneFromFixedRealm retrieveSceneFromFixedRealm;
        private readonly RetrieveSceneFromVolatileWorld retrieveSceneFromVolatileWorld;
        private readonly TeleportController teleportController;
        private GlobalWorld globalWorld;

        public RealmController(IWebRequestController webRequestController, TeleportController teleportController, RetrieveSceneFromFixedRealm retrieveSceneFromFixedRealm, RetrieveSceneFromVolatileWorld retrieveSceneFromVolatileWorld, int sceneLoadRadius,
            IReadOnlyList<int2> staticLoadPositions, RealmData realmData)
        {
            this.webRequestController = webRequestController;
            this.sceneLoadRadius = sceneLoadRadius;
            this.staticLoadPositions = staticLoadPositions;
            this.realmData = realmData;
            this.teleportController = teleportController;
            this.retrieveSceneFromFixedRealm = retrieveSceneFromFixedRealm;
            this.retrieveSceneFromVolatileWorld = retrieveSceneFromVolatileWorld;
        }

        public void SetupWorld(GlobalWorld globalWorld)
        {
            this.globalWorld = globalWorld;
            teleportController.OnWorldLoaded(globalWorld.EcsWorld);
        }

        /// <summary>
        ///     it is an async process so it should be executed before ECS kicks in
        /// </summary>
        public async UniTask SetRealmAsync(URLDomain realm, CancellationToken ct)
        {
            World world = globalWorld.EcsWorld;

            // Show loading screen

            await UnloadCurrentRealmAsync();

            IpfsTypes.ServerAbout result = await (await webRequestController.GetAsync(new CommonArguments(realm.Append(new URLPath("/about"))), ct, ReportCategory.REALM))
               .OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            realmData.Reconfigure(new IpfsRealm(realm, result));

            // Add the realm component
            var realmComp = new RealmComponent(realmData);

            Entity realmEntity = world.Create(realmComp,
                new ParcelsInRange(new HashSet<int2>(100), sceneLoadRadius), ProcessesScenePointers.Create());

            if (!ComplimentWithStaticPointers(world, realmEntity) && !realmComp.ScenesAreFixed)
                ComplimentWithVolatilePointers(world, realmEntity);

            teleportController.OnRealmLoaded(realmData.ScenesAreFixed ? retrieveSceneFromFixedRealm : retrieveSceneFromVolatileWorld);

            // Hide loading screen
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

        public async UniTask UnloadCurrentRealmAsync()
        {
            World world = globalWorld.EcsWorld;

            FindLoadedScenes(world);

            // release pooled entities
            for (var i = 0; i < globalWorld.FinalizeWorldSystems.Count; i++)
                globalWorld.FinalizeWorldSystems[i].FinalizeComponents(world.Query(in CLEAR_QUERY));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            globalWorld.Clear();

            teleportController.InvalidateRealm();
            realmData.Invalidate();

            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));

            // Collect garbage, good moment to do it
            GC.Collect();
        }

        public async UniTask DisposeGlobalWorldAsync()
        {
            World world = globalWorld.EcsWorld;
            FindLoadedScenes(world);

            // Destroy everything without awaiting as it's Application Quit
            globalWorld.Dispose();

            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));
        }

        private void FindLoadedScenes(World world)
        {
            // Dispose all scenes
            allScenes.Clear();

            // find all loaded scenes
            world.Query(in SCENES, (ref ISceneFacade scene) => allScenes.Add(scene));
        }
    }
}
