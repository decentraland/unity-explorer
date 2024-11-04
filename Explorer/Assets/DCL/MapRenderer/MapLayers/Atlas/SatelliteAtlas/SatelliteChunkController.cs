using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using DG.Tweening;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    public class SatelliteChunkController : IChunkController
    {
        private const string CHUNKS_API = "https://media.githubusercontent.com/media/genesis-city/genesis.city/master/map/latest/3/";
        private static readonly Color FINAL_COLOR = Color.white;
        private static readonly Color INITIAL_COLOR = new (0, 0, 0, 0);
        private readonly MapRendererTextureContainer textureContainer;

        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private readonly AtlasChunk atlasChunk;

        private CancellationTokenSource? internalCts;
        private CancellationTokenSource? linkedCts;
        private int webRequestAttempts;

        private IOwnedTexture2D? currentOwnedTexture;

        public SatelliteChunkController(
            SpriteRenderer prefab,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            MapRendererTextureContainer textureContainer,
            Vector3 chunkLocalPosition,
            Vector2Int coordsCenter,
            Transform parent,
            int drawOrder)
        {
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
            this.textureContainer = textureContainer;
            internalCts = new CancellationTokenSource();

            atlasChunk = Object.Instantiate(prefab, parent).GetComponent<AtlasChunk>();
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
            webRequestAttempts = 0;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, ct);
            atlasChunk.MainSpriteRenderer.enabled = false;
            atlasChunk.MainSpriteRenderer.color = INITIAL_COLOR;
            var url = $"{CHUNKS_API}{chunkId.x}%2C{chunkId.y}.jpg";

            currentOwnedTexture?.Dispose();
            currentOwnedTexture = null;

            currentOwnedTexture = (
                await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url)),
                    getTextureArgsFactory.NewArguments(TextureType.Albedo),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Trilinear)
                                        .SuppressExceptionsWithFallback(OwnedTexture2D.NewEmptyTexture(), reportContext: ReportCategory.UI),
                    linkedCts.Token, ReportCategory.UI
                )
            )!;
            Texture2D texture = currentOwnedTexture.Texture;

            textureContainer.AddChunk(chunkId, texture);

            float pixelsPerUnit = texture.width / chunkWorldSize;

            atlasChunk.MainSpriteRenderer.enabled = true;
            atlasChunk.LoadingSpriteRenderer.DOColor(INITIAL_COLOR, 0.5f).OnComplete(() => atlasChunk.LoadingSpriteRenderer.gameObject.SetActive(false));
            atlasChunk.MainSpriteRenderer.DOColor(FINAL_COLOR, 0.5f);

            atlasChunk.MainSpriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, pixelsPerUnit,
                0, SpriteMeshType.FullRect, Vector4.one, false);

            atlasChunk.MainSpriteRenderer.sprite.name = chunkId.ToString();
        }
    }
}
