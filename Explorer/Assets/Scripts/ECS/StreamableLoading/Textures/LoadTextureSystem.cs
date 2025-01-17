using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Plugins.TexturesFuse.TexturesServerWrap;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : PartialDownloadSystemBase<Texture2DData, GetTextureIntention>
    {
        private readonly ITexturesFuse texturesFuse;

        public LoadTextureSystem(
            World world,
            IStreamableCache<Texture2DData, GetTextureIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            ITexturesFuse texturesFuse
            ) : base(world, cache, webRequestController, buffersPool)
        {
            this.texturesFuse = texturesFuse;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> ProcessCompletedData(byte[] completeData, GetTextureIntention intention, IPartitionComponent partition, CancellationToken ct, StreamableLoadingState state)
        {
            EnumResult<IOwnedTexture2D,NativeMethods.ImageResult> textureFromBytesAsync = await texturesFuse.TextureFromBytesAsync(completeData, TextureType.Albedo, ct);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(textureFromBytesAsync.Value));
        }
    }
}
