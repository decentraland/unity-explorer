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
using Utility.Types;

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
            {
                // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
                result = await webRequestController.GetTextureAsync(
                                                        intention.CommonArguments,
                                                        new GetTextureArguments(intention.TextureType),
                                                        GetReportData()
                                                    )
                                                   .CreateTextureAsync(intention.WrapMode, intention.FilterMode, ct);
            }

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }

        private async UniTask<IOwnedTexture2D?> TryResolveAvatarTextureAsync(URLAddress url, GetTextureIntention intention, CancellationToken ct)
        {
            var newCommonArgs = new CommonArguments(url, AVATAR_TEXTURE_MAX_ATTEMPTS, StreamableLoadingDefaults.TIMEOUT, AVATAR_TEXTURE_REQUEST_DELAY_MS);

            var textureArguments = new GetTextureArguments(intention.TextureType);

            Result<IOwnedTexture2D> result = await webRequestController.GetTextureAsync(
                                                                            newCommonArgs,
                                                                            textureArguments,
                                                                            GetReportData(),
                                                                            suppressErrors: true
                                                                        )
                                                                       .CreateTextureAsync(intention.WrapMode, intention.FilterMode, ct)
                                                                       .SuppressToResultAsync(GetReportCategory());

            if (result.Success) return result.Value;

            CommonLoadingArguments newArgs = intention.CommonArguments;
            newArgs.URL = url;
            intention.CommonArguments = newArgs;

            return await webRequestController.GetTextureAsync(
                                                  intention.CommonArguments,
                                                  textureArguments,
                                                  GetReportData()
                                              )
                                             .CreateTextureAsync(intention.WrapMode, intention.FilterMode, ct);
        }
    }
}
