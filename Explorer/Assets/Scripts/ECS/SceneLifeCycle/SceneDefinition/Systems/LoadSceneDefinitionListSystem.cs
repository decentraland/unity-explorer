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
using Ipfs;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a scene list originated from pointers
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionListSystem : LoadSystemBase<SceneDefinitions, GetSceneDefinitionList>
    {
        private readonly IWebRequestController webRequestController;

        // cache
        private readonly StringBuilder bodyBuilder = new ();

        // There is no cache for the list but a cache per entity that is stored in ECS itself
        internal LoadSceneDefinitionListSystem(World world, IWebRequestController webRequestController,
            IStreamableCache<SceneDefinitions, GetSceneDefinitionList> cache)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneDefinitions>> FlowInternalAsync(GetSceneDefinitionList intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (var i = 0; i < intention.Pointers.Count; ++i)
            {
                int2 pointer = intention.Pointers[i];

                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(pointer.x);
                bodyBuilder.Append(',');
                bodyBuilder.Append(pointer.y);
                bodyBuilder.Append('\"');

                if (i != intention.Pointers.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            List<SceneEntityDefinition> targetList = await
                webRequestController.PostAsync(intention.CommonArguments, GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct)
               .OverwriteFromJsonAsync(intention.TargetCollection, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            return new StreamableLoadingResult<SceneDefinitions>(new SceneDefinitions(targetList));
        }
    }
}
