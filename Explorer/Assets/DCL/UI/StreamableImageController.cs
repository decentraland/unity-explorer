using System;
using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DG.Tweening;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    /// <summary>
    ///     A drop-in replacement for ImageController that uses the ECS Streamable Loading pipeline.
    ///     Benefits: Deduplication, Caching (Memory/Disk), Budgeting, and Automatic Memory Release.
    /// </summary>
    public class StreamableImageController : IDisposable
    {
        private static readonly Color LOADING_COLOR = new(0, 0, 0, 0);
        private const int PIXELS_PER_UNIT = 50;

        private readonly ImageView view;
        private readonly World world;
        private readonly Color defaultColor = Color.white;

        // Track the entity holding the AssetPromise. 
        // As long as this entity exists, the Texture ref-count is > 0.
        private Entity currentLoadingEntity = Entity.Null;
        private CancellationTokenSource cts = new();

        public event Action<Sprite>? SpriteLoaded;

        public StreamableImageController(ImageView view, World world)
        {
            this.view = view;
            this.world = world;
        }

        public void RequestImage(string uri, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            RequestImage(uri, defaultColor, removePrevious, hideImageWhileLoading, useKtx, fitAndCenterImage, defaultSprite);
        }

        public void RequestImage(string uri, Color targetColor, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            if (removePrevious)
                view.Image.sprite = null;

            if (hideImageWhileLoading)
                view.Image.enabled = false;

            // restart the token for the new request
            cts = cts.SafeRestart();

            RequestImageAsync(uri, useKtx, targetColor, cts.Token, fitAndCenterImage, defaultSprite).Forget();
        }

        public void SetVisible(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
        }

        private async UniTask RequestImageAsync(string uri, bool useKtx, Color targetColor, CancellationToken ct, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            // Release the previous image (decrements ref count in cache)
            CleanUpCurrentEntity();

            try
            {
                view.Image.color = LOADING_COLOR;
                view.IsLoading = true;

                // Define the Intention
                // The Cache system uses this Intention to check if the asset is already loaded or downloading.
                var intention = new GetTextureIntention(
                    url: uri,
                    fileHash: string.Empty,
                    wrapMode: TextureWrapMode.Clamp,
                    filterMode: FilterMode.Bilinear,
                    textureType: TextureType.Albedo,
                    reportSource: "UI_StreamableImageController"
                );

                // Create the Promise Entity
                // TOP_PRIORITY ensures UI assets skip the distance check and get budget priority.
                var promise = AssetPromise<TextureData, GetTextureIntention>.Create(
                    world,
                    intention,
                    PartitionComponent.TOP_PRIORITY
                );

                currentLoadingEntity = promise.Entity;

                // Wait for ECS to resolve the loading
                // We use ToUniTaskWithoutDestroyAsync because we want to keep the entity alive
                // to hold the reference to the texture.
                promise = await promise.ToUniTaskWithoutDestroyAsync(world, cancellationToken: ct);

                if (promise.TryGetResult(world, out var result) && result.Succeeded)
                {
                    // 5. Create Sprite from cached Texture
                    var texture = result.Asset!.EnsureTexture2D();

                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        VectorUtilities.OneHalf,
                        PIXELS_PER_UNIT,
                        0,
                        SpriteMeshType.FullRect,
                        Vector4.one,
                        false
                    );

                    ApplySprite(sprite, targetColor, fitAndCenterImage);
                }
                else
                {
                    // Handle Failure
                    var ex = result.Exception ?? new Exception($"Failed to load image: {uri}");
                    if (ex is not OperationCanceledException)
                        ReportHub.LogException(ex, ReportCategory.UI);

                    if (defaultSprite != null)
                        TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
                }
            }
            catch (OperationCanceledException)
            {
                // Request cancelled, likely by a new RequestImage call
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
            }
            finally
            {
                view.IsLoading = false;

                // If cancelled mid-flight, clean up the entity
                // immediately to avoid hanging references
                if (ct.IsCancellationRequested)
                    CleanUpCurrentEntity();
            }
        }

        private void CleanUpCurrentEntity()
        {
            if (currentLoadingEntity == Entity.Null) return;

            if (world.IsAlive(currentLoadingEntity))
            {
                // This tag tells ECS to dispose of this promise and decrement the 
                // texture reference count in the next CleanUp loop.
                world.Add(currentLoadingEntity, new DeleteEntityIntention());
            }

            currentLoadingEntity = Entity.Null;
        }

        private void ApplySprite(Sprite sprite, Color targetColor, bool fitAndCenterImage)
        {
            SetImage(sprite, fitAndCenterImage);
            SpriteLoaded?.Invoke(sprite);
            view.Image.enabled = true;
            view.Image.DOColor(targetColor, view.imageLoadingFadeDuration);
        }

        private void TryApplyDefaultSprite(Sprite? defaultSprite, bool fitAndCenterImage)
        {
            if (defaultSprite == null) return;

            SetImage(defaultSprite, fitAndCenterImage);
            view.Image.enabled = true;
            view.Image.DOColor(defaultColor, view.imageLoadingFadeDuration);
        }

        public void SetImage(Sprite sprite, bool fitAndCenterImage = false)
        {
            view.SetImage(sprite, fitAndCenterImage);
        }

        public void StopLoading()
        {
            cts.SafeCancelAndDispose();
            CleanUpCurrentEntity();
            view.IsLoading = false;
        }

        public void Dispose()
        {
            StopLoading();
        }
    }
}