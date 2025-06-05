using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Threading;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    public class ParcelChunkController : IChunkController
    {
        private const int PIXELS_PER_UNIT = 50;
        private const int LOCAL_SCALE_LOADING_SPRITE = 8;


        private readonly SpriteRenderer spriteRenderer;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private IOwnedTexture2D? currentOwnedTexture;
        private readonly AtlasChunk atlasChunk;

        private Uri chunksAPI => decentralandUrlsSource.Url(DecentralandUrl.ApiChunks);

        public ParcelChunkController(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            SpriteRenderer prefab,
            Vector3 chunkLocalPosition,
            Vector2Int coordsCenter
        )
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            spriteRenderer = prefab;
            atlasChunk = prefab.GetComponent<AtlasChunk>();
            atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(true);
            atlasChunk.MainSpriteRenderer.enabled = false;
            atlasChunk.LoadingSpriteRenderer.transform.localScale = Vector3.one * LOCAL_SCALE_LOADING_SPRITE;

#if UNITY_EDITOR
            prefab.gameObject.name = $"Chunk {coordsCenter.x},{coordsCenter.y}";
#endif

            atlasChunk.transform.localScale = Vector3.one * PIXELS_PER_UNIT;
            atlasChunk.transform.localPosition = chunkLocalPosition;
        }

        public void Dispose()
        {
            if (spriteRenderer)
                UnityObjectUtils.SafeDestroy(spriteRenderer.gameObject);
        }

        public async UniTask LoadImageAsync(int chunkSize, int parcelSize, Vector2Int mapPosition, CancellationToken ct)
        {
            atlasChunk.MainSpriteRenderer.color = AtlasChunkConstants.INITIAL_COLOR;

            var url = $"{chunksAPI}?center={mapPosition.x},{mapPosition.y}&width={chunkSize}&height={chunkSize}&size={parcelSize}";
            var textureTask = webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(TextureType.Albedo),
                ReportCategory.UI
            );

            Texture2D texture;

            currentOwnedTexture?.Dispose();
            currentOwnedTexture = null;

            try
            {
                currentOwnedTexture = await textureTask.CreateTextureAsync(TextureWrapMode.Clamp, FilterMode.Trilinear, ct);
                await UniTask.SwitchToMainThread();
                texture = currentOwnedTexture.Texture;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                texture = Texture2D.whiteTexture;
            }

            spriteRenderer.sprite =
                Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

            atlasChunk.MainSpriteRenderer.enabled = true;
            atlasChunk.LoadingSpriteRenderer.DOColor(AtlasChunkConstants.INITIAL_COLOR, 0.5f).OnComplete(() => atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(false));
            atlasChunk.MainSpriteRenderer.DOColor(AtlasChunkConstants.FINAL_COLOR, 0.5f);
        }

        public void SetDrawOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
        }
    }
}
