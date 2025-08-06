using System;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.ComponentsFactory;
using DCL.WebRequests;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using Utility;
using UnityEngine.AddressableAssets;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    public class SatelliteChunkController : IChunkController
    {
        private const float SATURATION_VALUE = 1f;
        private const string CHUNKS_API = "https://media.githubusercontent.com/media/genesis-city/parcels/new-client-images/maps/lod-0/3/";

        private readonly MapRendererTextureContainer textureContainer;

        private readonly IWebRequestController webRequestController;
        private readonly AtlasChunk atlasChunk;

        private CancellationTokenSource? internalCts;
        private CancellationTokenSource? linkedCts;
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");

        private IOwnedTexture2D? currentOwnedTexture;

        public SatelliteChunkController(
            SpriteRenderer prefab,
            IWebRequestController webRequestController,
            MapRendererTextureContainer textureContainer,
            Vector3 chunkLocalPosition,
            Vector2Int coordsCenter,
            int drawOrder)
        {
            this.webRequestController = webRequestController;
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

            currentOwnedTexture?.Dispose();
            currentOwnedTexture = null;

            if (atlasChunk)
                UnityObjectUtils.SafeDestroy(atlasChunk.gameObject);
        }

        public async UniTask LoadImageAsync(Vector2Int chunkId, float chunkWorldSize, CancellationToken ct)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, ct);
            atlasChunk.MainSpriteRenderer.enabled = false;
            atlasChunk.MainSpriteRenderer.color = AtlasChunkConstants.INITIAL_COLOR;
            var url = $"{CHUNKS_API}{chunkId.x}%2C{chunkId.y}.jpg";

            var textureTask = webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url), RetryPolicy.WithRetries(1)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Trilinear),
                linkedCts.Token,
                ReportCategory.UI
            );

            Texture2D texture;

            currentOwnedTexture?.Dispose();
            currentOwnedTexture = null;

            try
            {
                currentOwnedTexture = await textureTask!;
                await UniTask.SwitchToMainThread();
                texture = currentOwnedTexture.Texture;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                texture = await Addressables.LoadAssetAsync<Texture2D>($"{chunkId.x},{chunkId.y}").Task;
            }

            textureContainer.AddChunk(chunkId, texture);

            float pixelsPerUnit = texture.width / chunkWorldSize;

            atlasChunk.MainSpriteRenderer.enabled = true;
            atlasChunk.LoadingSpriteRenderer.DOColor(AtlasChunkConstants.INITIAL_COLOR, 0.5f).OnComplete(() => atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(false));
            atlasChunk.MainSpriteRenderer.DOColor(AtlasChunkConstants.FINAL_COLOR, 0.5f);

            atlasChunk.MainSpriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, pixelsPerUnit,
                0, SpriteMeshType.FullRect, Vector4.one, false);

            atlasChunk.MainSpriteRenderer.sprite.name = chunkId.ToString();
        }
    }
}
