using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Thirdweb.Unity
{
    public class MyEmailLoginModal : AbstractEmailModal
    {
        [field: SerializeField] [field: Header("UI Settings")]
        private Canvas OTPCanvas { get; set; }

        [field: SerializeField]
        private TMP_InputField OTPInputField { get; set; }

        [field: SerializeField]
        private Button OTPSubmitButton { get; set; }

        public override async Task<string> GetEmailAsync()
        {
            await UniTask.SwitchToMainThread();

            if (OTPCanvas != null)
                OTPCanvas.gameObject.SetActive(true);

            await UniTask.NextFrame();

            if (OTPInputField != null)
            {
                OTPInputField.interactable = true;
                OTPInputField.readOnly = false;
                OTPInputField.text = string.Empty;
                OTPInputField.Select();
                OTPInputField.ActivateInputField();

                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(OTPInputField.gameObject);
            }

            var tcs = new UniTaskCompletionSource<string>();

            if (OTPSubmitButton != null)
                OTPSubmitButton.onClick.AddListener(OnSubmit);

            if (OTPInputField != null)
                OTPInputField.onSubmit.AddListener(OnSubmitFromField);

            try
            {
                string email = await tcs.Task;
                return email;
            }
            finally
            {
                if (OTPSubmitButton != null)
                    OTPSubmitButton.onClick.RemoveListener(OnSubmit);

                if (OTPInputField != null)
                    OTPInputField.onSubmit.RemoveListener(OnSubmitFromField);

                if (OTPCanvas != null)
                    OTPCanvas.gameObject.SetActive(false);
            }

            void OnSubmit()
            {
                Debug.Log("VVV email submit pressed");
                string text = OTPInputField != null ? (OTPInputField.text ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrEmpty(text)) return;
                if (!text.Contains("@")) return;
                tcs.TrySetResult(text);
            }

            void OnSubmitFromField(string _)
            {
                OnSubmit();
            }
        }
    }
}
