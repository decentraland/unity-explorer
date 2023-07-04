using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using Ipfs;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SceneLifeCycleGroup))]
    [UpdateAfter(typeof(ResolveScenesStateSystem))]
    public partial class StartSceneSystem : BaseUnityLoopSystem
    {
        private readonly CancellationToken destroyCancellationToken;

        private readonly SceneLifeCycleState state;
        private readonly ISceneFactory sceneFactory;

        public StartSceneSystem(World world, SceneLifeCycleState state, ISceneFactory sceneFactory, CancellationToken destroyCancellationToken) : base(world)
        {
            this.sceneFactory = sceneFactory;
            this.state = state;
            this.destroyCancellationToken = destroyCancellationToken;
        }

        protected override void Update(float t)
        {
            ProcessSceneToLoadQuery(World);
        }

        private async UniTask InitializeSceneAndStart(SceneLoadingComponent sceneLoadingComponent, CancellationToken ct)
        {
            // main thread
            ISceneFacade sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(state.IpfsRealm, sceneLoadingComponent.Definition, sceneLoadingComponent.AssetBundleManifest, ct);

            ct.RegisterWithoutCaptureExecutionContext(() => sceneFacade?.DisposeAsync().Forget());

            // thread pool
            await sceneFacade.StartUpdateLoop(30, ct);
        }

        [Query]
        private void ProcessSceneToLoad(in Entity entity, ref SceneLoadingComponent sceneLoadingComponent)
        {
            // If the scene just spawned, we start the request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            var liveSceneComponent = new LiveSceneComponent
            {
                CancellationTokenSource = cts,
                Task = InitializeSceneAndStart(sceneLoadingComponent, cts.Token),
            };

            World.Add(entity, liveSceneComponent);

            World.Remove<SceneLoadingComponent>(entity);
        }
    }
}
