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
        private const int AVATAR_TEXTURE_MAX_ATTEMPTS = 4;
        private const int AVATAR_TEXTURE_REQUEST_DELAY_MS = 5000;

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

            IOwnedTexture2D? result = null;

            if (intention.IsAvatarTexture)
            {
                URLAddress? url = await avatarTextureUrlProvider.GetAsync(intention.CommonArguments.URL.Value, ct);

                if (url == null)
                    throw new Exception($"No profile found for {intention.CommonArguments.URL}");

                result = await TryResolveAvatarTextureAsync(url.Value, intention, ct);
            }
            else
                // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
                result = await webRequestController.GetTextureAsync(
                    intention.CommonArguments,
                    new GetTextureArguments(intention.TextureType),
                    GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode),
                    ct,
                    GetReportData()
                );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }

        private async UniTask<IOwnedTexture2D?> TryResolveAvatarTextureAsync(URLAddress url, GetTextureIntention intention, CancellationToken ct)
        {
            CommonArguments newCommonArgs = new CommonArguments(url, AVATAR_TEXTURE_MAX_ATTEMPTS, StreamableLoadingDefaults.TIMEOUT, AVATAR_TEXTURE_REQUEST_DELAY_MS);

            GetTextureWebRequest.CreateTextureOp textureOp = GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode);
            GetTextureArguments textureArguments = new GetTextureArguments(intention.TextureType);

            IOwnedTexture2D? result = null;

            result = await webRequestController.GetTextureAsync(
                newCommonArgs,
                textureArguments,
                textureOp,
                ct,
                GetReportData(),
                suppressErrors: true,
                ignoreIrrecoverableErrors: true
            ).SuppressAnyExceptionWithFallback(null);

            if (result != null) return result;

            CommonLoadingArguments newArgs = intention.CommonArguments;
            newArgs.URL = url;
            intention.CommonArguments = newArgs;

            result = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                textureArguments,
                textureOp,
                ct,
                GetReportData()
            );

            return result;
        }
    }
}
