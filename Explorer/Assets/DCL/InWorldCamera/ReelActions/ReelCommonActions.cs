using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.InWorldCamera.ReelActions
{
    public static class ReelCommonActions
    {
        private const string DECENTRALAND_REELS_HOME_FOLDER = "decentraland/reels";

        /// <summary>
        ///     Opens a browser tab on x.com with a tweet ready to be posted containing the reel url.
        ///     Also copies the url to the clipboard.
        /// </summary>
        public static void ShareReelToX(string shareToXMessage, string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard, IWebBrowser webBrowser)
        {
            string description = shareToXMessage;
            string url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{reelId}";
            string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

            systemClipboard.Set(xUrl);
            webBrowser.OpenUrl(xUrl);
        }

        /// <summary>
        ///     Copies the reel url to the clipboard.
        /// </summary>
        public static void CopyReelLink(string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard)
        {
            systemClipboard.Set($"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{reelId}");
        }

        /// <summary>
        ///     Downloads a reel image to local storage in {home_directory}/{DECENTRALAND_REELS_HOME_FOLDER}/{reelId}
        ///     and opens the default file browser at that location
        /// </summary>
        public static async UniTask DownloadReelToFileAsync(IWebRequestController webRequestController, string reelUrl, CancellationToken ct)
        {
            using IOwnedTexture2D texture = await webRequestController.GetTextureAsync(reelUrl, new GetTextureArguments(TextureType.Albedo), ReportCategory.CAMERA_REEL)
                                                                      .CreateTextureAsync(TextureWrapMode.Clamp, ct: ct)
                                                                      .WithCustomExceptionAsync(e => new Exception("Error while downloading reel", e));

            {
                Uri uri = new Uri(reelUrl);

                StringBuilder absolutePathBuilder = new StringBuilder();
                byte[] imageBytes = texture.Texture.EncodeToPNG();

                absolutePathBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                                   .Append("/")
                                   .Append(DECENTRALAND_REELS_HOME_FOLDER)
                                   .Append("/")
                                   .Append(Path.GetFileName(uri.LocalPath))
                                   .Replace(" ", "\\ ");

                string absolutePath = absolutePathBuilder.ToString();
                string directoryPath = Path.GetDirectoryName(absolutePath);

                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                await File.WriteAllBytesAsync(absolutePath, imageBytes, ct);
            }
        }
    }
}
