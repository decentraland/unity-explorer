using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    public enum ToastMessageType
    {
        SUCCESS,
        FAILURE
    }
    
    public class GenericToastMessage : MonoBehaviour
    {
        [SerializeField] private WarningNotificationView toastView;
        [SerializeField] private GameObject successToastIcon;
        [SerializeField] private GameObject failureToastIcon;
        [SerializeField] private float toastDuration = 3f;
        [SerializeField] private string successToastDefaultMessage = "Success!";
        [SerializeField] private string failureToastDefaultMessage = "Failure!";

        private CancellationTokenSource toastCts = new ();

        public void ShowToastMessage(ToastMessageType type, string? message)
        {
            HideToast();
            
            if(string.IsNullOrEmpty(message))
                message = type == ToastMessageType.SUCCESS ? successToastDefaultMessage : failureToastDefaultMessage;
            
            successToastIcon.SetActive(type == ToastMessageType.SUCCESS);
            failureToastIcon.SetActive(type == ToastMessageType.FAILURE);

            ShowToastAsync(message, toastCts.Token).Forget();
        }

        private async UniTaskVoid ShowToastAsync(string message, CancellationToken ct)
        {
            toastView.SetText(message);
            toastView.Show(ct);
            await UniTask.Delay((int) toastDuration * 1000, cancellationToken: ct);
            toastView.Hide(false, ct);
        }

        private void HideToast()
        {
            toastCts = toastCts.SafeRestart();
            toastView.CanvasGroup.DOKill();
            toastView.CanvasGroup.alpha = 0f;
        }
    }
}
