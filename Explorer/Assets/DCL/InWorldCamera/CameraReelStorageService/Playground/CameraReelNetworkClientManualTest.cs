using CommunicationData.URLHelpers;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.Playground
{
    public class CameraReelNetworkClientManualTest : MonoBehaviour
    {
        public DecentralandEnvironment Env;

        public CameraReelStorageResponse Storage;

        [Header("GALLERY")]
        public int Limit = 10;
        public int Offset;

        [Space(5)]
        public CameraReelResponses Result;
        public Texture2D ImageTexture;
        public Texture2D ThumbnailTexture;

        [Header("UPLOAD")]
        public string ThumbnailUrl;

        private CameraReelWebRequestClient client
        {
            get
            {
                DecentralandUrlsSource urlsSource = new DecentralandUrlsSource(Env);
                return new CameraReelWebRequestClient(webRequestController, urlsSource);
            }
        }

        private readonly IWeb3IdentityCache.Default identity = new ();
        private readonly IWebRequestController webRequestController = IWebRequestController.DEFAULT;

        [ContextMenu(nameof(GET_STORAGE))]
        public async void GET_STORAGE()
        {
            Storage = await client.GetUserGalleryStorageInfoRequest(identity.Identity.Address, default(CancellationToken));
        }

        [ContextMenu(nameof(GET_GALLERY))]
        public async void GET_GALLERY()
        {
            Result = await client.GetScreenshotGalleryRequest(identity.Identity.Address, Limit, Offset, default(CancellationToken));

            Storage.currentImages = Result.currentImages;
            Storage.maxImages = Result.maxImages;

            ImageTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(Result.images.First().url)),
                new GetTextureArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), default(CancellationToken), ReportCategory.CAMERA_REEL);

            ThumbnailTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(Result.images.First().thumbnailUrl)),
                new GetTextureArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), default(CancellationToken), ReportCategory.CAMERA_REEL);
        }

        [ContextMenu(nameof(UPLOAD_IMAGE))]
        public async void UPLOAD_IMAGE()
        {
            CameraReelUploadResponse response = await client.UploadScreenshotRequest(
                ImageTexture.EncodeToJPG(), Result.images.First().metadata, default(CancellationToken));

            Storage.currentImages = response.currentImages;
            Storage.maxImages = response.maxImages;

            ThumbnailUrl = response.image.thumbnailUrl;
        }

        [ContextMenu(nameof(DELETE_IMAGE))]
        public async void DELETE_IMAGE()
        {
            Storage = await client.DeleteScreenshotRequest(Result.images.First().id, default(CancellationToken));
        }
    }
}
