using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.Textures;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTImageSystem : LoadSystemBase<TextureData, GetNFTImageIntention>
    {
        public LoadNFTImageSystem(World world, IStreamableCache<TextureData, GetNFTImageIntention> cache)
            : base(world, cache)
        {
        }

        protected override async UniTask<StreamableLoadingResult<TextureData>> FlowInternalAsync(GetNFTImageIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var getTexture = new GetTextureIntention(intention.CommonArguments.URL, string.Empty,
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, nameof(LoadNFTImageSystem), 1);

            var promise = AssetPromise<TextureData, GetTextureIntention>.Create(World, getTexture, partition);

            promise = await promise.ToUniTaskAsync(World, cancellationToken: ct);

            return promise.Result!.Value;
        }
    }
}
