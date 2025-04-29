using Cysharp.Threading.Tasks;
using DCL.UI;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelToast
{
    public enum CameraReelToastMessageType
    {
        FAILURE,
        SUCCESS,
        DOWNLOAD
    }

    public class CameraReelToastMessage : MonoBehaviour
    {
        [field: SerializeField] public WarningNotificationView SuccessToastView { get; private set; }
        [field: SerializeField] public WarningNotificationView FailureToastView { get; private set; }
        [field: SerializeField] public DownloadNotificationView DownloadToastView { get; private set; }
        [field: SerializeField] public float SuccessToastDuration { get; private set; } = 3f;
        [field: SerializeField] public float FailureToastDuration { get; private set; } = 3f;
        [field: SerializeField] public string FailureToastDefaultMessage { get; private set; } = "There was an error while trying to process your request. Please try again!";
        [field: SerializeField] public string SuccessToastDefaultMessage { get; private set; } = "Success!";

        private CancellationTokenSource showSuccessCts = new ();
        private CancellationTokenSource showFailureCts = new ();
        private CancellationTokenSource showDownloadCts = new ();

        private void HideSuccessNotification()
        {
            showSuccessCts = showSuccessCts.SafeRestart();
            SuccessToastView.CanvasGroup.DOKill();
            SuccessToastView.CanvasGroup.alpha = 0f;
        }

        private void HideFailureNotification()
        {
            showFailureCts = showFailureCts.SafeRestart();
            FailureToastView.CanvasGroup.DOKill();
            FailureToastView.CanvasGroup.alpha = 0f;
        }

        private void HideDownloadNotification()
        {
            showDownloadCts = showDownloadCts.SafeRestart();
            var canvasGroup = DownloadToastView.NotificationView.CanvasGroup;
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
        }

        public void ShowToastMessage(CameraReelToastMessageType type, string? message = null)
        {
            HideSuccessNotification();
            HideFailureNotification();
            HideDownloadNotification();

            switch (type)
            {
                case CameraReelToastMessageType.SUCCESS:
                    ShowNotificationAsync(message, SuccessToastDefaultMessage, SuccessToastView, SuccessToastDuration, showSuccessCts.Token).Forget();
                    break;
                case CameraReelToastMessageType.FAILURE:
                    ShowNotificationAsync(message, FailureToastDefaultMessage, FailureToastView, FailureToastDuration, showFailureCts.Token).Forget();
                    break;
                case CameraReelToastMessageType.DOWNLOAD:
                    DownloadToastView.PrepareToBeClicked();

                    ShowNotificationAsync(message, SuccessToastDefaultMessage,
                            DownloadToastView.NotificationView, FailureToastDuration,
                            showFailureCts.Token)
                       .Forget();

                    break;
            }
        }

        private async UniTask ShowNotificationAsync(string? message, string defaultMessage, WarningNotificationView notificationView, float duration, CancellationToken ct)
        {
            notificationView.SetText(message ?? defaultMessage);
            notificationView.Show(ct);
            await UniTask.Delay((int) duration * 1000, cancellationToken: ct);
            notificationView.Hide(false, ct);
        }

        private void OnDisable()
        {
            HideSuccessNotification();
            HideFailureNotification();
            HideDownloadNotification();
        }
    }
}
