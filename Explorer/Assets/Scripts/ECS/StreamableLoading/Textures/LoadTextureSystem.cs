using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2D, GetTextureIntention>
    {
        internal LoadTextureSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache, MutexSync mutexSync
          , IConcurrentBudgetProvider loadingFrameTimeBudgetProvider) : base(world, cache, mutexSync, loadingFrameTimeBudgetProvider) { }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternal(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            using UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(intention.CommonArguments.URL, !intention.IsReadable);
            await webRequest.SendWebRequest().WithCancellation(ct);
            Texture2D tex = DownloadHandlerTexture.GetContent(webRequest);
            tex.wrapMode = intention.WrapMode;
            tex.filterMode = intention.FilterMode;
            return new StreamableLoadingResult<Texture2D>(tex);
        }
    }
}
