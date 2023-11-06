using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PerformanceBudgeting.AcquiredBudget;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
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
        internal LoadTextureSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
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
