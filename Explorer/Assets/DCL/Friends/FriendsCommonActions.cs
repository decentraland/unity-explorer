using DCL.Browser;

namespace DCL.Friends
{
    public static class FriendsCommonActions
    {
        private const string REPORT_USER_URL = "https://docs.google.com/forms/d/e/1FAIpQLSdpetm5TWVt2gjc27LJ96wl5JLR2bB9m5O-9KqDrvMYvB3Vpw/viewform";

        public static void ReportPlayer(IWebBrowser webBrowser)
        {
            webBrowser.OpenUrl(REPORT_USER_URL);
        }
    }
}
