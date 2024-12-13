using DCL.Browser;
using DCL.Clipboard;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace DCL.InWorldCamera.ReelActions
{
    public static class ReelCommonActions
    {
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
        ///     Opens a browser tab to the full resolution reel url
        /// </summary>
        public static void DownloadReel(string reelUrl, IWebBrowser webBrowser)
        {
            webBrowser.OpenUrl(reelUrl);
        }
    }
}
