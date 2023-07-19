using Arch.Core;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.BudgetProvider;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Ipfs;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Pool;

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

        private readonly int sceneLoadRadius;
        private readonly IReadOnlyList<int2> staticLoadPositions;
        private readonly CameraSamplingData cameraSamplingData;

        public RealmController(int sceneLoadRadius, IReadOnlyList<int2> staticLoadPositions, CameraSamplingData cameraSamplingData)
        {
            this.sceneLoadRadius = sceneLoadRadius;
            this.staticLoadPositions = staticLoadPositions;
            this.cameraSamplingData = cameraSamplingData;
        }

        /// <summary>
        ///     it is an async process so it should be executed before ECS kicks in
        /// </summary>
        public async UniTask SetRealm(GlobalWorld globalWorld, string realm, CancellationToken ct)
        {
            World world = globalWorld.EcsWorld;

            // Show loading screen

            await UnloadCurrentRealm(globalWorld);

            async UniTask<StreamableLoadingResult<IpfsTypes.ServerAbout>> CreateServerAboutRequest(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
            {
                UnityWebRequest wr = await UnityWebRequest.Get(intention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                string text = wr.downloadHandler.text;

                await UniTask.SwitchToThreadPool();
                JsonUtility.FromJsonOverwrite(text, serverAbout);
                await UniTask.SwitchToMainThread();
                return new StreamableLoadingResult<IpfsTypes.ServerAbout>(serverAbout);
            }

            var intent = new SubIntention(new CommonLoadingArguments(realm + "/about"));
            IpfsTypes.ServerAbout result = (await intent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, CreateServerAboutRequest, ReportCategory.REALM, ct)).UnwrapAndRethrow();

            // Add the realm component
            var realmComp = new RealmComponent(new IpfsRealm(realm, result));

            Entity realmEntity = world.Create(realmComp, new ParcelsInRange(new HashSet<int2>(100), sceneLoadRadius));

            ComplimentWithStaticPointers(world, realmEntity);

            if (!ComplimentWithStaticPointers(world, realmEntity) && !realmComp.ScenesAreFixed)
                ComplimentWithVolatilePointers(world, realmEntity);

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

        public async UniTask UnloadCurrentRealm(GlobalWorld globalWorld)
        {
            World world = globalWorld.EcsWorld;

            // Dispose all scenes
            allScenes.Clear();

            // find all loaded scenes
            world.Query(in SCENES, (ref ISceneFacade scene) => allScenes.Add(scene));
            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));

            // release pooled entities
            for (var i = 0; i < globalWorld.FinalizeWorldSystems.Count; i++)
                globalWorld.FinalizeWorldSystems[i].FinalizeComponents(world.Query(in CLEAR_QUERY));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            globalWorld.Clear();

            // Collect garbage, good moment to do it
            GC.Collect();
        }
    }
}
