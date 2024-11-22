using DCL.Browser;
using DCL.Clipboard;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace DCL.InWorldCamera.ReelActions
{
    public class ReelCommonActions
    {
        public static void ShareReelToX(string shareToXMessage, string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard, IWebBrowser webBrowser)
        {
            string description = shareToXMessage.Replace(" ", "%20");
            string url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{reelId}";
            string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

            systemClipboard.Set(xUrl);
            webBrowser.OpenUrl(xUrl);
        }

        public static void CopyReelLink(string reelId, IDecentralandUrlsSource decentralandUrlsSource, ISystemClipboard systemClipboard)
        {
            systemClipboard.Set($"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{reelId}");
        }

        public static void DownloadReel(string reelUrl, IWebBrowser webBrowser)
        {
            webBrowser.OpenUrl(reelUrl);
        }
    }
}
