using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using MVC;
using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace DCL.ApplicationBlocklistGuard
{
    public class BlockedScreenController : ControllerBase<BlockedScreenView, BlockedScreenParameters>
    {
        private const string DEFAULT_INFO_TEXT = "Please contact support for more information.";

        private readonly IWebBrowser webBrowser;
        private readonly StringBuilder infoTextBuilder = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public BlockedScreenController(ViewFactoryMethod viewFactory, IWebBrowser webBrowser) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
        }

        protected override void OnViewInstantiated()
        {
            if (viewInstance != null)
            {
                viewInstance.CloseButton.onClick.AddListener(ExitUtils.Exit);
                viewInstance.SupportButton.onClick.AddListener(OnSupportClicked);
            }
        }

        protected override void OnBeforeViewShow()
        {
            infoTextBuilder.Clear();

            if (inputData.BannedUserData != null)
            {
                infoTextBuilder.Append("Ban period: ");
                infoTextBuilder.Append(FormatRemainingBanTime(inputData.BannedUserData.expiresAt));
                infoTextBuilder.AppendLine();
                infoTextBuilder.Append("Reason: ");
                infoTextBuilder.Append(inputData.BannedUserData.reason);
                infoTextBuilder.AppendLine();
            }
            infoTextBuilder.Append(DEFAULT_INFO_TEXT);

            viewInstance!.InfoText.text = infoTextBuilder.ToString();
        }

        public override void Dispose()
        {
            infoTextBuilder.Clear();

            if (viewInstance == null)
                return;

            viewInstance.CloseButton.onClick.RemoveListener(ExitUtils.Exit);
            viewInstance.SupportButton.onClick.RemoveListener(OnSupportClicked);
        }

        private void OnSupportClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
        }

        private static string FormatRemainingBanTime(string expiresAt)
        {
            if (!DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime expirationUtc))
                return expiresAt;

            TimeSpan remaining = expirationUtc - DateTime.UtcNow;

            if (remaining.TotalSeconds <= 0)
                return "expired";

            if (remaining.TotalDays >= 1)
            {
                var days = (int)remaining.TotalDays;
                return days == 1 ? "1 day" : $"{days} days";
            }

            var hours = (int)Math.Ceiling(remaining.TotalHours);
            return hours <= 1 ? "1h" : $"{hours}h";
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
