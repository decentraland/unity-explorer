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

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SceneLifeCycleGroup))]
    [UpdateAfter(typeof(ResolveScenesStateSystem))]
    public partial class StartSceneSystem : BaseUnityLoopSystem
    {
        private readonly CancellationToken destroyCancellationToken;

        private readonly IIpfsRealm ipfsRealm;
        private readonly ISceneFactory sceneFactory;

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

        private async UniTask InitializeSceneAndStart(SceneLoadingComponent sceneLoadingComponent, CancellationToken ct)
        {
            ISceneFacade sceneFacade;

            try
            {
                // main thread
                sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(ipfsRealm, sceneLoadingComponent.Definition, sceneLoadingComponent.AssetBundleManifest, ct);
                ct.RegisterWithoutCaptureExecutionContext(() => sceneFacade?.DisposeAsync().Forget());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.SCENE_FACTORY));
                return;
            }

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
