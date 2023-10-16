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
using System.Threading;
using Newtonsoft.Json;
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
        internal LoadSceneDefinitionSystem(World world, IStreamableCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition> cache, MutexSync mutexSync)
            : base(world, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>> FlowInternal(GetSceneDefinition intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string text;

            using (var wr = UnityWebRequest.Get(intention.CommonArguments.URL))
            {
                await wr.SendWebRequest().WithCancellation(ct);

                // Get text on the main thread
                text = wr.downloadHandler.text;
            }

            await UniTask.SwitchToThreadPool();

#if UNITY_EDITOR
            //Note (Juani): In editor I get way less crashes while using this JsonConvert method. So, nto to affect the build,
            //I added this directive
            IpfsTypes.SceneEntityDefinition sceneEntityDefinition = JsonConvert.DeserializeObject<IpfsTypes.SceneEntityDefinition>(text);
#else
            IpfsTypes.SceneEntityDefinition sceneEntityDefinition = JsonUtility.FromJson<IpfsTypes.SceneEntityDefinition>(text);
#endif
            sceneEntityDefinition.id ??= intention.IpfsPath.EntityId;

            // switching back is handled by the base class
            return new StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>(sceneEntityDefinition);
        }
    }
}
