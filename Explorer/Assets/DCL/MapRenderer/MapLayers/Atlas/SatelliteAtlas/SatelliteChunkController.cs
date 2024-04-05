using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer.ComponentsFactory;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    public class SatelliteChunkController : IChunkController
    {
        private const string CHUNKS_API = "https://media.githubusercontent.com/media/genesis-city/genesis.city/master/map/latest/3/";

        private readonly SpriteRenderer spriteRenderer;
        private readonly IWebRequestController webRequestController;
        private readonly MapRendererTextureContainer textureContainer;

        private CancellationTokenSource internalCts;
        private CancellationTokenSource linkedCts;
        private int webRequestAttempts;

        public SatelliteChunkController(SpriteRenderer prefab, IWebRequestController webRequestController, MapRendererTextureContainer textureContainer, Vector3 chunkLocalPosition, Vector2Int coordsCenter,
            Transform parent,
            int drawOrder)
        {
            this.webRequestController = webRequestController;
            this.textureContainer = textureContainer;
            internalCts = new CancellationTokenSource();

            spriteRenderer = Object.Instantiate(prefab, parent);
            spriteRenderer.transform.localPosition = chunkLocalPosition;
            spriteRenderer.sortingOrder = drawOrder;

#if UNITY_EDITOR
            spriteRenderer.gameObject.name = $"Chunk {coordsCenter.x},{coordsCenter.y}";
#endif
        }

        public void Dispose()
        {
            internalCts?.Cancel();
            linkedCts?.Dispose();
            linkedCts = null;

            internalCts?.Dispose();
            internalCts = null;

            if (spriteRenderer)
                UnityObjectUtils.SafeDestroy(spriteRenderer.gameObject);
        }

        public async UniTask LoadImageAsync(Vector2Int chunkId, float chunkWorldSize, CancellationToken ct)
        {
            webRequestAttempts = 0;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, ct);

            var url = $"{CHUNKS_API}{chunkId.x}%2C{chunkId.y}.jpg";

            Texture2D texture = (await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(url)), new GetTextureArguments(false), linkedCts.Token))
               .CreateTexture(TextureWrapMode.Clamp, FilterMode.Trilinear);

            texture.name = chunkId.ToString();

            textureContainer.AddChunk(chunkId, texture);

            float pixelsPerUnit = texture.width / chunkWorldSize;

            spriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, pixelsPerUnit,
                0, SpriteMeshType.FullRect, Vector4.one, false);
        }
    }
}
