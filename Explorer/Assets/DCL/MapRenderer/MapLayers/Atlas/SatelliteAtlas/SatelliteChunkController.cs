using System;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.ComponentsFactory;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using Utility;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    public class SatelliteChunkController : IChunkController
    {
        private const float SATURATION_VALUE = 1f;

        private readonly MapRendererTextureContainer textureContainer;
        private readonly AtlasChunk atlasChunk;

        private CancellationTokenSource? internalCts;
        private CancellationTokenSource? linkedCts;
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");

        private AsyncOperationHandle<Texture2D> bundledTextureHandle;

        public SatelliteChunkController(
            SpriteRenderer prefab,
            MapRendererTextureContainer textureContainer,
            Vector3 chunkLocalPosition,
            Vector2Int coordsCenter,
            int drawOrder)
        {
            this.textureContainer = textureContainer;
            internalCts = new CancellationTokenSource();

            prefab.material.SetFloat(SATURATION, SATURATION_VALUE);
            atlasChunk = prefab.GetComponent<AtlasChunk>();
            atlasChunk.transform.localPosition = chunkLocalPosition;
            atlasChunk.LoadingSpriteRenderer.sortingOrder = drawOrder;
            atlasChunk.MainSpriteRenderer.sortingOrder = drawOrder;
            atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(true);

#if UNITY_EDITOR
            atlasChunk.gameObject.name = $"Chunk {coordsCenter.x},{coordsCenter.y}";
#endif
        }

        public void Dispose()
        {
            internalCts?.Cancel();
            linkedCts?.Dispose();
            linkedCts = null;

            internalCts?.Dispose();
            internalCts = null;

            ReleaseTexture();

            if (atlasChunk)
                UnityObjectUtils.SafeDestroy(atlasChunk.gameObject);
        }

        public async UniTask LoadImageAsync(Vector2Int chunkId, float chunkWorldSize, CancellationToken ct)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, ct);
            CancellationToken loadCt = linkedCts.Token;
            atlasChunk.MainSpriteRenderer.enabled = false;
            atlasChunk.MainSpriteRenderer.color = AtlasChunkConstants.INITIAL_COLOR;

            ReleaseTexture();

            bundledTextureHandle = Addressables.LoadAssetAsync<Texture2D>($"{chunkId.x},{chunkId.y}");
            await bundledTextureHandle.Task;

            if (loadCt.IsCancellationRequested)
            {
                ReleaseTexture();
                return;
            }

            if (bundledTextureHandle.Status != AsyncOperationStatus.Succeeded)
            {
                ReportHub.LogError(ReportCategory.UI, $"Satellite chunk {chunkId} failed to load from Addressables");
                ReleaseTexture();
                return;
            }

            Texture2D texture = bundledTextureHandle.Result;

            // Closing this in try catch, because SpriteRenderer on application closing is being disposed before this code executes.
            try
            {
                float pixelsPerUnit = texture.width / chunkWorldSize;

                atlasChunk.MainSpriteRenderer.enabled = true;
                atlasChunk.LoadingSpriteRenderer.DOColor(AtlasChunkConstants.INITIAL_COLOR, 0.5f).OnComplete(() => atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(false));
                atlasChunk.MainSpriteRenderer.DOColor(AtlasChunkConstants.FINAL_COLOR, 0.5f);

                atlasChunk.MainSpriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, pixelsPerUnit,
                    0, SpriteMeshType.FullRect, Vector4.one, false);

                atlasChunk.MainSpriteRenderer.sprite.name = chunkId.ToString();
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                throw;
            }

            textureContainer.AddChunk(chunkId, texture);
        }

        private void ReleaseTexture()
        {
            if (bundledTextureHandle.IsValid())
                Addressables.Release(bundledTextureHandle);

            bundledTextureHandle = default;
        }
    }
}
