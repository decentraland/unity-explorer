using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Linq;
using System.Threading;

namespace ECS.SceneLifeCycle
{
    public partial class ECSUnloadAllScenes : IUnloadAllScenes
    {
        private readonly IScenesCache scenesCache;
        private World? world;

        public ECSUnloadAllScenes(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public void Initialize(World world)
        {
            this.world = world;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (world == null) return;

            ct.ThrowIfCancellationRequested();
            RequestUnloadAllScenesQuery(world);

            await UniTask.WaitUntil(() => scenesCache.Scenes.All(facade => facade.SceneStateProvider.State == SceneState.Disposed), cancellationToken: ct);
        }

        [Query]
        [Any(typeof(ISceneFacade),
            typeof(SceneDefinitionComponent),
            typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        [None(typeof(DeleteEntityIntention))]
        private void RequestUnloadAllScenes(in Entity entity)
        {
            world!.Add<DeleteEntityIntention>(entity);
        }
    }
}
