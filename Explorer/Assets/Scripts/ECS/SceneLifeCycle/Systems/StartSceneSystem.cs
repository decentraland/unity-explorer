using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using Ipfs;
using SceneRunner;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LoadSceneSystem))]
    public partial class StartSceneSystem : BaseUnityLoopSystem
    {
        private readonly ISceneFactory sceneFactory;

        private readonly IIpfsRealm ipfsRealm;

        private readonly CancellationToken destroyCancellationToken;

        public StartSceneSystem(World world, IIpfsRealm ipfsRealm, ISceneFactory sceneFactory, CancellationToken destroyCancellationToken) : base(world)
        {
            this.sceneFactory = sceneFactory;
            this.ipfsRealm = ipfsRealm;
            this.destroyCancellationToken = destroyCancellationToken;
        }

        protected override void Update(float t)
        {
            ProcessSceneToLoadQuery(World);
        }

        private async UniTask InitializeSceneAndStart(IpfsTypes.SceneEntityDefinition sceneDefinition, CancellationToken ct)
        {
            // main thread
            var sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(ipfsRealm, sceneDefinition, ct);

            var disposeScene = new Action(() =>
            {
                sceneFacade?.DisposeAsync();
            });

            ct.RegisterWithoutCaptureExecutionContext(disposeScene);

            destroyCancellationToken.RegisterWithoutCaptureExecutionContext(disposeScene);

            // thread pool
            await sceneFacade.StartUpdateLoop(30, ct);
        }

        [Query]
        private void ProcessSceneToLoad(in Entity entity, ref SceneLoadingComponent sceneLoadingComponent)
        {
            // If the scene just spawned, we start the request
            sceneLoadingComponent.CancellationTokenSource = new CancellationTokenSource();
            var cts = sceneLoadingComponent.CancellationTokenSource;

            var liveSceneComponent = new LiveSceneComponent()
            {
                CancellationTokenSource = cts,
            };

            World.Add(entity, liveSceneComponent);

            sceneLoadingComponent.Request = InitializeSceneAndStart(sceneLoadingComponent.Definition, sceneLoadingComponent.CancellationTokenSource.Token);

            World.Remove<SceneLoadingComponent>(entity);
        }
    }
}
