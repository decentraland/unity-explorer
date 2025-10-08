using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.Textures;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTVideoSystem : LoadSystemBase<Texture2DData, GetNFTVideoIntention>
    {
        private readonly ExtendedObjectPool<Texture2D> videoTexturePool;

        public LoadNFTVideoSystem(World world,
            IStreamableCache<Texture2DData, GetNFTVideoIntention> cache,
            ExtendedObjectPool<Texture2D> videoTexturePool)
            : base(world, cache)
        {
            this.videoTexturePool = videoTexturePool;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTVideoIntention intention,
            StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var texture2D = videoTexturePool.Get();
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture2D, intention.CommonArguments.URL));
        }
    }
}
