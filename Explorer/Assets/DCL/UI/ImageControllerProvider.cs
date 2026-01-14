using System;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public readonly struct Texture2DRef : IDisposable
    {
        private readonly StreamableRefCountData<AnyTexture>.RefAcquisition refAcquisition;
        public readonly Texture2D Texture;

        public Texture2DRef(TextureData textureData, Texture2D texture)
        {
            refAcquisition = textureData.AcquireRef();
            Texture = texture;
        }

        public void Dispose()
        {
            refAcquisition.Dispose();
        }
    }

    public class ImageControllerProvider
    {
        private readonly World world;

        public ImageControllerProvider(World world)
        {
            this.world = world;
        }

        public ImageController Create(ImageView view)
        {
            return new ImageController(view, this);
        }

        public async UniTask<Texture2DRef?> LoadTextureAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url)) return null;

            var intention = new GetTextureIntention(
                url: url,
                fileHash: string.Empty,
                wrapMode: TextureWrapMode.Clamp,
                filterMode: FilterMode.Bilinear,
                textureType: TextureType.Albedo,
                reportSource: "ImageControllerProvider"
            );

            var promise = AssetPromise<TextureData, GetTextureIntention>.Create(
                world,
                intention,
                PartitionComponent.TOP_PRIORITY
            );

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (promise.TryGetResult(world, out var result) && result.Succeeded)
            {
                var textureData = result.Asset!;
                var texture = textureData.EnsureTexture2D();

                var textureRef = new Texture2DRef(textureData, texture);
                textureData.Dereference();

                return textureRef;
            }

            if (result.Exception != null && result.Exception is not OperationCanceledException)
                ReportHub.LogException(result.Exception, ReportCategory.UI);

            return null;
        }
    }
}
