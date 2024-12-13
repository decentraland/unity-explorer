using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.IO;
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
            string description = shareToXMessage.Replace(" ", "%20");
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
        public static async UniTask DownloadReelToFileAsync(string reelUrl, CancellationToken ct)
        {
            using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(reelUrl))
            {
                Uri uri = new Uri(reelUrl);
                await webRequest.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (webRequest.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Error while downloading reel: {webRequest.error}");

                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                byte[] imageBytes = texture.EncodeToPNG();
                string pathToReelsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DECENTRALAND_REELS_HOME_FOLDER);
                string absolutePath = Path.Combine(pathToReelsFolder, Path.GetFileName(uri.LocalPath));

                string directoryPath = Path.GetDirectoryName(absolutePath);

                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                await File.WriteAllBytesAsync(absolutePath, imageBytes, ct);

                OpenFolderExplorer(absolutePath);
            }
        }

        private static void OpenFolderExplorer(string path)
        {
            string sanitizedPath = path.Replace(" ", "\\ ");
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                System.Diagnostics.Process.Start("explorer.exe", "/select," + sanitizedPath);
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                System.Diagnostics.Process.Start("open", "-R " + sanitizedPath);
            else if (Application.platform == RuntimePlatform.LinuxPlayer)
                System.Diagnostics.Process.Start("xdg-open", sanitizedPath);
        }
    }
}
