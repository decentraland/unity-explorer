using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a single scene definition from URN
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionSystem : LoadSystemBase<SceneEntityDefinition, GetSceneDefinition>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadSceneDefinitionSystem(World world, IWebRequestController webRequestController, IStreamableCache<SceneEntityDefinition, GetSceneDefinition> cache, MutexSync mutexSync)
            : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneEntityDefinition>> FlowInternalAsync(GetSceneDefinition intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            SceneEntityDefinition sceneEntityDefinition = await
                webRequestController.GetAsync(intention.CommonArguments, ct, GetReportCategory())
               .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            sceneEntityDefinition.id ??= intention.IpfsPath.EntityId;

            // switching back is handled by the base class
            return new StreamableLoadingResult<SceneEntityDefinition>(sceneEntityDefinition);
        }
    }
}
