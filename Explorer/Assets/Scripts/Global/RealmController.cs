using Arch.Core;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Ipfs;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Pool;

namespace Global
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
        private readonly List<Vector2Int> staticLoadPositions;

        public RealmController(int sceneLoadRadius, List<Vector2Int> staticLoadPositions)
        {
            this.sceneLoadRadius = sceneLoadRadius;
            this.staticLoadPositions = staticLoadPositions;
        }

        /// <summary>
        ///     it is an async process so it should be executed before ECS kicks in
        /// </summary>
        public async UniTask SetRealm(World world, string realm, CancellationToken ct)
        {
            // Show loading screen

            await UnloadCurrentRealm(world);

            async UniTask<StreamableLoadingResult<IpfsTypes.ServerAbout>> CreateServerAboutRequest(SubIntention intention, CancellationToken ct)
            {
                UnityWebRequest wr = await UnityWebRequest.Get(intention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                string text = wr.downloadHandler.text;

                await UniTask.SwitchToThreadPool();
                JsonUtility.FromJsonOverwrite(text, serverAbout);
                await UniTask.SwitchToMainThread();
                return new StreamableLoadingResult<IpfsTypes.ServerAbout>(serverAbout);
            }

            var intent = new SubIntention(new CommonLoadingArguments(realm + "/about"));
            IpfsTypes.ServerAbout result = (await intent.RepeatLoop(CreateServerAboutRequest, ReportCategory.REALM, ct)).UnwrapAndRethrow();

            // Add the realm component
            var realmComp = new RealmComponent(new IpfsRealm(realm, result));

            Entity realmEntity = world.Create(realmComp, new ParcelsInRange(new HashSet<Vector2Int>(100), sceneLoadRadius));

            if (staticLoadPositions is { Count: > 0 })

                // Static scene pointers don't replace the logic of fixed pointers loading but compliment it
                world.Add(realmEntity, new StaticScenePointers(staticLoadPositions));

            // Hide loading screen
        }

        public async UniTask UnloadCurrentRealm(World world)
        {
            // Dispose all scenes
            allScenes.Clear();

            // find all loaded scenes
            world.Query(in SCENES, (ref ISceneFacade scene) => allScenes.Add(scene));
            await UniTask.WhenAll(allScenes.Select(s => s.DisposeAsync()));

            // Clear the world from everything connected to the current realm
            world.Destroy(in CLEAR_QUERY);

            // Collect garbage, good moment to do it
            GC.Collect();
        }
    }
}
