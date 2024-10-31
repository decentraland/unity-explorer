using DCL.Browser.DecentralandUrls;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.Playground
{
    public class CameraReelStoragePlayground : MonoBehaviour
    {
        private readonly IWeb3IdentityCache.Default identity = new ();
        public DecentralandEnvironment env;

        [Header("STORAGE")]
        public int CurrentImages;
        public int MaxImages;

        [Header("GALLERY")]
        public int Limit = 10;
        public int Offset;

        [Header("DELETE")]
        public string ImageUid = "09f01d43-140f-4c8b-ad63-40bac3dc187d";

        [Header("UPLOAD")]
        public Texture2D uploadTexture;
        public string thumbnailUrl;

        private CameraReelWebRequestClient client;

        public void Initialize()
        {
            var urlsSource = new DecentralandUrlsSource(env);
            client = new CameraReelWebRequestClient(IWebRequestController.DEFAULT, urlsSource);
        }

        [ContextMenu(nameof(GET_STORAGE))]
        public async void GET_STORAGE()
        {
            Initialize();

            CameraReelStorageResponse result = await client.GetUserGalleryStorageInfoRequest(identity.Identity.Address, default(CancellationToken));

            CurrentImages = result.currentImages;
            MaxImages = result.maxImages;
        }

        [ContextMenu(nameof(GET_GALLERY))]
        public async void GET_GALLERY()
        {
            Initialize();

            CameraReelResponses result = await client.GetScreenshotGalleryRequest(identity.Identity.Address, Limit, Offset, default(CancellationToken));

            CurrentImages = result.currentImages;
            MaxImages = result.maxImages;
        }

        [ContextMenu(nameof(GET_AND_UPLOAD_IMAGE))]
        public async void GET_AND_UPLOAD_IMAGE()
        {
            Initialize();

            CameraReelResponses result = await client.GetScreenshotGalleryRequest(identity.Identity.Address, Limit, Offset, default(CancellationToken));

            CameraReelUploadResponse response = await client.UploadScreenshotRequest(
                uploadTexture.EncodeToJPG(), result.images.First().metadata, default(CancellationToken));

            thumbnailUrl = response.image.thumbnailUrl;
            CurrentImages = response.currentImages;
            MaxImages = response.maxImages;
        }

        [ContextMenu(nameof(DELETE_IMAGE))]
        public async void DELETE_IMAGE()
        {
            Initialize();

            CameraReelStorageResponse result = await client.DeleteScreenshotRequest(ImageUid, default(CancellationToken));

            CurrentImages = result.currentImages;
            MaxImages = result.maxImages;
        }
    }
}
