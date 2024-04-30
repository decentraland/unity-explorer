using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    public class ParcelChunkController : IChunkController
    {
        private const int PIXELS_PER_UNIT = 50;
        private const string CHUNKS_API = "https://api.decentraland.org/v1/map.png";

        private readonly SpriteRenderer spriteRenderer;

        private readonly IWebRequestController webRequestController;

        public ParcelChunkController(IWebRequestController webRequestController, SpriteRenderer prefab, Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent)
        {
            this.webRequestController = webRequestController;

            spriteRenderer = Object.Instantiate(prefab, parent);
#if UNITY_EDITOR
            spriteRenderer.gameObject.name = $"Chunk {coordsCenter.x},{coordsCenter.y}";
#endif
            Transform transform = spriteRenderer.transform;

            transform.localScale = Vector3.one * PIXELS_PER_UNIT;
            transform.localPosition = chunkLocalPosition;
        }

        public void Dispose()
        {
            if (spriteRenderer)
                UnityObjectUtils.SafeDestroy(spriteRenderer.gameObject);
        }

        public async UniTask LoadImageAsync(int chunkSize, int parcelSize, Vector2Int mapPosition, CancellationToken ct)
        {
            var url = $"{CHUNKS_API}?center={mapPosition.x},{mapPosition.y}&width={chunkSize}&height={chunkSize}&size={parcelSize}";

            Texture2D texture =
                (await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(url)),
                    new GetTextureArguments(false),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct)).Texture;

            spriteRenderer.sprite =
                Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public void SetDrawOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
        }
    }
}
