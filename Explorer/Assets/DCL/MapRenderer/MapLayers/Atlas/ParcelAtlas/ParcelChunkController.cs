using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    public class ParcelChunkController : IChunkController
    {
        private const int PIXELS_PER_UNIT = 50;

        private readonly SpriteRenderer spriteRenderer;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private IOwnedTexture2D? currentOwnedTexture;
        private string CHUNKS_API => decentralandUrlsSource.Url(DecentralandUrl.ApiChunks);

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
#if UNITY_EDITOR
            prefab.gameObject.name = $"Chunk {coordsCenter.x},{coordsCenter.y}";
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
            var textureTask = webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Trilinear),
                ct,
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
                texture = Texture2D.whiteTexture;
            }

            spriteRenderer.sprite =
                Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public void SetDrawOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
        }
    }
}