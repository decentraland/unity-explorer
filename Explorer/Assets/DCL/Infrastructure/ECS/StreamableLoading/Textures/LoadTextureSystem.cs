using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.Unity.Textures.Utils;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2DData, GetTextureIntention>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IAvatarTextureUrlProvider avatarTextureUrlProvider;

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache,
            // A replacement of IProfileRepository to avoid cyclic dependencies
            IAvatarTextureUrlProvider avatarTextureUrlProvider)
            : base(
                world, cache, new DiskCacheOptions<Texture2DData, GetTextureIntention>(diskCache, GetTextureIntention.DiskHashCompute.INSTANCE, "tex")
            )
        {
            this.webRequestController = webRequestController;
            this.avatarTextureUrlProvider = avatarTextureUrlProvider;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.IsVideoTexture) throw new NotSupportedException($"{nameof(LoadTextureSystem)} does not support video textures. They should be handled by {nameof(VideoTextureUtils)}");

            if (intention.IsAvatarTexture)
            {
                URLAddress? url = await avatarTextureUrlProvider.GetAsync(intention.CommonArguments.URL.Value, ct);

                if (url == null)
                    throw new Exception($"No profile found for {intention.CommonArguments.URL}");

                CommonLoadingArguments newArgs = intention.CommonArguments;
                newArgs.URL = url.Value;
                intention.CommonArguments = newArgs;
            }

            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                new GetTextureArguments(intention.TextureType),
                GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode),
                ct,
                GetReportData()
            );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }
    }
}
