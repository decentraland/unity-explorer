using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserErrorNotificationService : IDisposable
    {
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly WarningNotificationView warningNotificationView;
        private CancellationTokenSource? currentErrorCts;

        public CommunitiesBrowserErrorNotificationService(WarningNotificationView warningNotificationView)
        {
            this.warningNotificationView = warningNotificationView;
        }

        public async UniTaskVoid ShowWarningNotification(string message)
        {
            currentErrorCts = currentErrorCts.SafeRestart();

            await warningNotificationView.AnimatedShowAsync(message, WARNING_MESSAGE_DELAY_MS, currentErrorCts.Token)
                                         .SuppressToResultAsync(ReportCategory.COMMUNITIES);
        }

        public void Dispose()
        {
            currentErrorCts?.SafeCancelAndDispose();
        }
    }
}
