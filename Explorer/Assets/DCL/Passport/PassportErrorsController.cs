using Cysharp.Threading.Tasks;
using DCL.UI;
using System.Threading;
using Utility;

namespace DCL.Passport
{
    public class PassportErrorsController
    {
        private const int HIDE_TIME_MS = 3000;
        private const string DEFAULT_MESSAGE = "There was an error while trying to process your request. Please try again!";

        private readonly WarningNotificationView errorNotification;

        private CancellationTokenSource showErrorCts;

        public PassportErrorsController(WarningNotificationView errorNotification)
        {
            this.errorNotification = errorNotification;
        }

        public void Show(string message = "")
        {
            showErrorCts = showErrorCts.SafeRestart();
            ShowErrorNotificationAsync(string.IsNullOrEmpty(message) ? DEFAULT_MESSAGE : message, showErrorCts.Token).Forget();
        }

        public void Hide(bool instant = false)
        {
            showErrorCts.SafeCancelAndDispose();
            errorNotification.Hide(instant);
        }

        private async UniTaskVoid ShowErrorNotificationAsync(string message, CancellationToken ct)
        {
            errorNotification.Text.text = message;
            errorNotification.Show();
            await UniTask.Delay(HIDE_TIME_MS, cancellationToken: ct);
            errorNotification.Hide();
        }
    }
}
