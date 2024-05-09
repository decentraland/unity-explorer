using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner;
using SceneRunner.Scene;
using System.Threading;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Loads a scene from scene and realm definitions
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneSystem : LoadSystemBase<ISceneFacade, GetSceneFacadeIntention>
    {
        private readonly ISceneFactory sceneFactory;
        private readonly LoadSceneSystemLogic loadSceneSystemLogic;
        private readonly LoadEmptySceneSystemLogic loadEmptySceneSystemLogic;

        internal LoadSceneSystem(World world,
            LoadSceneSystemLogic loadSceneSystemLogic, LoadEmptySceneSystemLogic loadEmptySceneSystemLogic,
            ISceneFactory sceneFactory, IStreamableCache<ISceneFacade, GetSceneFacadeIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.sceneFactory = sceneFactory;
            this.loadSceneSystemLogic = loadSceneSystemLogic;
            this.loadEmptySceneSystemLogic = loadEmptySceneSystemLogic;
        }

        protected override async UniTask<StreamableLoadingResult<ISceneFacade>> FlowInternalAsync(GetSceneFacadeIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct) =>
            intention.DefinitionComponent.IsEmpty
                ? new StreamableLoadingResult<ISceneFacade>(loadEmptySceneSystemLogic.Flow(intention))
                : new StreamableLoadingResult<ISceneFacade>(await loadSceneSystemLogic.FlowAsync(sceneFactory, intention, GetReportCategory(), partition, ct));

        protected override void DisposeAbandonedResult(ISceneFacade asset)
        {
            asset.DisposeAsync().Forget();
        }

        public override void Dispose()
        {
            base.Dispose();
            loadEmptySceneSystemLogic.Dispose();
        }
    }
}
