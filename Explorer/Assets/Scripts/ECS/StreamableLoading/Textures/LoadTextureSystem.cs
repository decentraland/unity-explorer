using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : PartialDownloadSystemBase<Texture2DData, GetTextureIntention>
    {
        public LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController)
            : base(world, cache, webRequestController)
        {
        }

        protected override StreamableLoadingResult<Texture2DData> ProcessCompletedData(byte[] completeData)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(completeData);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture));
        }
    }
}
