using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a scene list originated from pointers
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionListSystem : LoadSystemBase<SceneDefinitions, GetSceneDefinitionList>
    {
        // cache
        private readonly StringBuilder bodyBuilder = new ();

        // There is no cache for the list but a cache per entity that is stored in ECS itself
        internal LoadSceneDefinitionListSystem(World world, IStreamableCache<SceneDefinitions, GetSceneDefinitionList> cache,
            MutexSync mutexSync, IConcurrentBudgetProvider concurrentBudgetProvider)
            : base(world, cache, mutexSync, concurrentBudgetProvider) { }

        protected override async UniTask<StreamableLoadingResult<SceneDefinitions>> FlowInternal(GetSceneDefinitionList intention, CancellationToken ct)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (var i = 0; i < intention.Pointers.Count; ++i)
            {
                Vector2Int pointer = intention.Pointers[i];

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

            var request = UnityWebRequest.Post(intention.CommonArguments.URL, bodyBuilder.ToString(), "application/json");
            await request.SendWebRequest().WithCancellation(ct);
            string text = request.downloadHandler.text;

            await UniTask.SwitchToThreadPool();

            List<IpfsTypes.SceneEntityDefinition> targetList = intention.TargetCollection;
            JsonConvert.PopulateObject(text, targetList);
            return new StreamableLoadingResult<SceneDefinitions>(new SceneDefinitions(targetList));
        }
    }
}
