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
        SUCCESS
    }

    public class CameraReelToastMessage : MonoBehaviour
    {
        [field: SerializeField] public WarningNotificationView SuccessToastView { get; private set; }
        [field: SerializeField] public WarningNotificationView FailureToastView { get; private set; }
        [field: SerializeField] public float SuccessToastDuration { get; private set; } = 3f;
        [field: SerializeField] public float FailureToastDuration { get; private set; } = 3f;
        [field: SerializeField] public string FailureToastDefaultMessage { get; private set; } = "There was an error while trying to process your request. Please try again!";
        [field: SerializeField] public string SuccessToastDefaultMessage { get; private set; } = "Success!";

        private CancellationTokenSource showSuccessCts = new ();
        private CancellationTokenSource showFailureCts = new ();

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

        public void ShowToastMessage(CameraReelToastMessageType type, string? message = null)
        {
            switch (type)
            {
                case CameraReelToastMessageType.SUCCESS:
                    ShowSuccessNotificationAsync(message).Forget();
                    break;
                case CameraReelToastMessageType.FAILURE:
                    ShowFailureNotificationAsync(message).Forget();
                    break;
            }
        }

        private async UniTask ShowSuccessNotificationAsync(string message)
        {
            HideSuccessNotification();
            HideFailureNotification();

            SuccessToastView.SetText(message ?? SuccessToastDefaultMessage);
            SuccessToastView.Show(showSuccessCts.Token);
            await UniTask.Delay((int) SuccessToastDuration * 1000, cancellationToken: showSuccessCts.Token);
            SuccessToastView.Hide(false, showSuccessCts.Token);
        }

        private async UniTask ShowFailureNotificationAsync(string message)
        {
            HideSuccessNotification();
            HideSuccessNotification();

            FailureToastView.SetText(message ?? FailureToastDefaultMessage);
            FailureToastView.Show(showFailureCts.Token);
            await UniTask.Delay((int) FailureToastDuration * 1000, cancellationToken: showFailureCts.Token);
            FailureToastView.Hide(false, showFailureCts.Token);
        }

        private void OnDisable()
        {
            HideSuccessNotification();
            HideFailureNotification();
        }
    }
}
