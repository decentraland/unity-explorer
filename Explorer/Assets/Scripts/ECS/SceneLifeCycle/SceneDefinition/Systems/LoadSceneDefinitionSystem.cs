using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;
using Newtonsoft.Json;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a single scene definition from URN
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionSystem : LoadSystemBase<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
    {
        internal LoadSceneDefinitionSystem(World world, MemoryBudgetProvider memoryBudgetProvider, IStreamableCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition> cache, MutexSync mutexSync)
            : base(world, memoryBudgetProvider, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>> FlowInternalAsync(GetSceneDefinition intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string text;

            using (var wr = UnityWebRequest.Get(intention.CommonArguments.URL))
            {
                await wr.SendWebRequest().WithCancellation(ct);

                // Get text on the main thread
                text = wr.downloadHandler.text;
            }

            await UniTask.SwitchToThreadPool();

            IpfsTypes.SceneEntityDefinition sceneEntityDefinition = Application.isEditor
                ? JsonConvert.DeserializeObject<IpfsTypes.SceneEntityDefinition>(text)
                : JsonUtility.FromJson<IpfsTypes.SceneEntityDefinition>(text);

            sceneEntityDefinition.id ??= intention.IpfsPath.EntityId;

            // switching back is handled by the base class
            return new StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>(sceneEntityDefinition);
        }
    }
}
