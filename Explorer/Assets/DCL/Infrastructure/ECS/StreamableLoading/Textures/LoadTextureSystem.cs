using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<TextureData, GetTextureIntention>
    {
        private const int AVATAR_TEXTURE_MAX_ATTEMPTS = 3;
        private const int AVATAR_TEXTURE_REQUEST_DELAY_MS = 5000;

        private readonly IWebRequestController webRequestController;
        private readonly IAvatarTextureUrlProvider avatarTextureUrlProvider;

        internal LoadTextureSystem(World world, IStreamableCache<TextureData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<TextureData> diskCache,
            // A replacement of IProfileRepository to avoid cyclic dependencies
            IAvatarTextureUrlProvider avatarTextureUrlProvider)
            : base(
                world, cache,
                new DiskCacheOptions<TextureData, GetTextureIntention>(diskCache, GetTextureIntention.DiskHashCompute.INSTANCE, "tex")
            )
        {
            this.webRequestController = webRequestController;
            this.avatarTextureUrlProvider = avatarTextureUrlProvider;
        }

        protected override async UniTask<StreamableLoadingResult<TextureData>> FlowInternalAsync(GetTextureIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.IsVideoTexture) throw new NotSupportedException($"{nameof(LoadTextureSystem)} does not support video textures. They should be handled by {nameof(MediaFactory)} synchronously");

            Texture2D? result;

            if (intention.IsAvatarTexture)
            {
                URLAddress? url = await avatarTextureUrlProvider.GetAsync(intention.AvatarTextureUserId!, ct);

                if (url == null)
                    throw new Exception($"No profile found for {intention.AvatarTextureUserId}");

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

            return new StreamableLoadingResult<TextureData>(new TextureData(AnyTexture.FromTexture2D(result.EnsureNotNull())));
        }

        private async UniTask<Texture2D?> TryResolveAvatarTextureAsync(URLAddress url, GetTextureIntention intention, CancellationToken ct)
        {
            var newCommonArgs = new CommonArguments(url, RetryPolicy.WithRetries(AVATAR_TEXTURE_MAX_ATTEMPTS, AVATAR_TEXTURE_REQUEST_DELAY_MS));

            GetTextureWebRequest.CreateTextureOp textureOp = GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode);
            GetTextureArguments textureArguments = new GetTextureArguments(intention.TextureType);

            var result = await webRequestController.GetTextureAsync(
                newCommonArgs,
                textureArguments,
                textureOp,
                ct,
                GetReportData(),
                suppressErrors: true,
                ignoreIrrecoverableErrors: true
            )!.SuppressAnyExceptionWithFallback(null);

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
