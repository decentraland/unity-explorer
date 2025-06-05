using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.ReelActions
{
    public static class ReelCommonActions
    {
        private const string DECENTRALAND_REELS_HOME_FOLDER = "Downloads";
        private static string reelsPath;

        /// <summary>
        ///     Opens a browser tab on x.com with a tweet ready to be posted containing the reel url.
        ///     Also copies the url to the clipboard.
        /// </summary>
        public static void ShareReelToX(string shareToXMessage, string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard, IWebBrowser webBrowser)
        {
            string description = shareToXMessage;
            var url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink).OriginalString}/{reelId}";
            string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

            systemClipboard.Set(xUrl);
            webBrowser.OpenUrl(new Uri(xUrl));
        }

        /// <summary>
        ///     Copies the reel url to the clipboard.
        /// </summary>
        public static void CopyReelLink(string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard)
        {
            systemClipboard.Set($"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink).OriginalString}/{reelId}");
        }

        /// <summary>
        ///     Downloads a reel image to local storage in {home_directory}/{DECENTRALAND_REELS_HOME_FOLDER}/{reelId}
        ///     and opens the default file browser at that location
        /// </summary>
        public static async UniTask DownloadReelToFileAsync(IWebRequestController webRequestController, Uri reelUrl, CancellationToken ct)
        {
            using IOwnedTexture2D texture = await webRequestController.GetTextureAsync(reelUrl, new GetTextureArguments(TextureType.Albedo), ReportCategory.CAMERA_REEL)
                                                                      .CreateTextureAsync(TextureWrapMode.Clamp, ct: ct)
                                                                      .WithCustomExceptionAsync(e => new Exception("Error while downloading reel", e));

            {
                byte[] imageBytes = texture.Texture.EncodeToPNG();
                string directoryPath = ReelsPath;
                string absolutePath = Path.Combine(ReelsPath, Path.GetFileName(reelUrl.LocalPath));

                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                await File.WriteAllBytesAsync(absolutePath, imageBytes, ct);
            }
        }

        public static string ReelsPath
        {
            get
            {
                if (reelsPath == null)
                {
                    reelsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        DECENTRALAND_REELS_HOME_FOLDER);
                }

                return reelsPath;
            }
        }
    }
}
