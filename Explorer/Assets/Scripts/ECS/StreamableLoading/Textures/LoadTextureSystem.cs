using Arch.Core;
using Arch.SystemGroups;
using CommunityToolkit.HighPerformance;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Profiling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Plugins.TexturesFuse.TexturesServerWrap;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using Utility;
using Utility.Types;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : PartialDownloadSystemBase<Texture2DData, GetTextureIntention>
    {
        private readonly ITexturesFuse texturesFuse;
        private readonly bool compressionEnabled;

        public LoadTextureSystem(
            World world,
            IStreamableCache<Texture2DData, GetTextureIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            ITexturesFuse texturesFuse,
            IDiskCache<Texture2DData> diskCache,
            bool compressionEnabled
            ) : base(world, cache, webRequestController, buffersPool, diskCache)
        {
            this.texturesFuse = texturesFuse;
            this.compressionEnabled = compressionEnabled;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> ProcessCompletedData(StreamableLoadingState state, GetTextureIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            ProfilingCounters.TexturesAmount.Value++;
            if (compressionEnabled)
            {
                EnumResult<IOwnedTexture2D, NativeMethods.ImageResult> textureFromBytesAsync = await texturesFuse.TextureFromBytesAsync(state.GetFullyDownloadedData(), intention.TextureType, ct);
                return new StreamableLoadingResult<Texture2DData>(new Texture2DData(textureFromBytesAsync.Value));
            }
            else
            {
                Texture2D texture = new Texture2D(1, 1);
                texture.LoadImage(state.GetFullyDownloadedData().ToArray());
                texture.wrapMode = intention.WrapMode;
                texture.filterMode = intention.FilterMode;
                texture.SetDebugName(intention.CommonArguments.URL.Value);
                return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture));
            }
        }

    }
}
