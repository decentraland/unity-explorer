using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    public class ParcelChunkController : IChunkController
    {
        private const int PIXELS_PER_UNIT = 50;

        private readonly SpriteRenderer spriteRenderer;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string CHUNKS_API => decentralandUrlsSource.Url(DecentralandUrl.ApiChunks);

        public ParcelChunkController(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            SpriteRenderer prefab,
            Vector3 chunkLocalPosition,
            Vector2Int coordsCenter,
            Transform parent
        )
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;

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
                await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(url)),
                    new GetTextureArguments(false),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp)
                                        .SuppressExceptionsWithFallback(Texture2D.whiteTexture, reportContext: ReportCategory.UI), ct, ReportCategory.UI);

            spriteRenderer.sprite =
                Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public void SetDrawOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
        }
    }
}
