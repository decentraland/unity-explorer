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
        private readonly LoadSceneSystemLogicBase loadSceneSystemLogic;

        internal LoadSceneSystem(World world,
            LoadSceneSystemLogicBase loadSceneSystemLogic,
            ISceneFactory sceneFactory, IStreamableCache<ISceneFacade, GetSceneFacadeIntention> cache) : base(world, cache)
        {
            this.sceneFactory = sceneFactory;
            this.loadSceneSystemLogic = loadSceneSystemLogic;
        }

        protected override async UniTask<StreamableLoadingResult<ISceneFacade>> FlowInternalAsync(GetSceneFacadeIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct) =>
            new (await loadSceneSystemLogic.FlowAsync(sceneFactory, intention, GetReportData(), partition, ct));

        protected override void DisposeAbandonedResult(ISceneFacade asset)
        {
            asset.DisposeAsync().Forget();
        }
    }
}
