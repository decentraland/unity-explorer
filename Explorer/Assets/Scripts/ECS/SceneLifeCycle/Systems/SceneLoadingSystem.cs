using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using Newtonsoft.Json;
using SceneRunner;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SceneLifeCycleSystem))]
    public partial class SceneLoadingSystem : BaseUnityLoopSystem
    {
        private readonly ISceneFactory sceneFactory;

        public SceneLoadingSystem(World world, ISceneFactory sceneFactory) : base(world)
        {
            this.sceneFactory = sceneFactory;
        }

        protected override void Update(float t)
        {
            ProcessSceneToLoadQuery(World);
        }

        private async UniTask InitializeSceneAndStart(Ipfs.SceneEntityDefinition sceneDefinition, CancellationToken ct)
        {
            // TODO: Use contentBaseUrl from realm
            const string CONTENT_BASE_URL = "https://sdk-test-scenes.decentraland.zone/content/contents/";

            // main thread
            var sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(CONTENT_BASE_URL, sceneDefinition, ct);

            ct.RegisterWithoutCaptureExecutionContext(() =>
            {
                sceneFacade?.DisposeAsync();
            });

            // thread pool
            await sceneFacade.StartUpdateLoop(30, ct);
        }

        [Query]
        [All(typeof(SceneLoadingComponent))]
        private void ProcessSceneToLoad(in Entity entity, ref SceneLoadingComponent sceneLoadingComponent)
        {
            // If the scene just spawned, we start the request
            sceneLoadingComponent.CancellationTokenSource = new CancellationTokenSource();
            var cts = sceneLoadingComponent.CancellationTokenSource;

            var liveSceneComponent = new LiveSceneComponent()
            {
                CancellationToken = cts,
                SceneLoop = sceneLoadingComponent.Request,
            };

            World.Add(entity, liveSceneComponent);

            sceneLoadingComponent.Request = InitializeSceneAndStart(sceneLoadingComponent.Definition, sceneLoadingComponent.CancellationTokenSource.Token);

            World.Remove<SceneLoadingComponent>(entity);
            Debug.Log("Spawned Scene: " + JsonConvert.SerializeObject(sceneLoadingComponent));
        }
    }
}
