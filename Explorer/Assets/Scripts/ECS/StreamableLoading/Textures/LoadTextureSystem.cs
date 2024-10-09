using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.Unity.Textures.Utils;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2DData, GetTextureIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.IsVideoTexture) throw new NotSupportedException($"{nameof(LoadTextureSystem)} does not support video textures. They should be handled by {nameof(VideoTextureUtils)}");

            //TODO remove
             // var texture= new Texture2D(10, 10, GraphicsFormat.R8_SInt, 10, TextureCreationFlags.Crunch);
             // Texture2D.CreateExternalTexture()
             // texture.LoadRawTextureData();

            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                new GetTextureArguments(intention.IsReadable),
                GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode),
                ct,
                GetReportData()
            );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
        }
    }
}
