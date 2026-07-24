using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [Serializable]
    public class Web3ConfirmationPopupConfig
    {
        public string ConfirmButtonText;
        public string Title;
        [Multiline]
        public string Description;
    }

    public class Web3ConfirmationPopupView : ViewBase
    {
        [Space]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;

        [Space]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueButtonText;

        [Header("TRANSACTION")]
        [SerializeField] private GameObject transactionInfoPanel;
        [Space]
        [SerializeField] private TMP_Text balanceValue;
        [SerializeField] private TMP_Text costValue;
        [SerializeField] private TMP_Text estimatedGasFeeValue;

        [Space]
        [SerializeField] private Web3ConfirmationPopupConfig transactionConfig;
        [SerializeField] private Web3ConfirmationPopupConfig signingConfig;

        [Header("RECIPIENT GATE")]
        [SerializeField] private TransactionRecipientPopupView recipientPopup;

        [Header("Can be Null")]
        [SerializeField] private GameObject? defaultContent;

        private void UseConfig(Web3ConfirmationPopupConfig config)
        {
            title.text = config.Title;
            description.text = config.Description;
            continueButtonText.text = config.ConfirmButtonText;
        }

        public async UniTask<bool> ShowAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            gameObject.SetActive(true);

            try
            {
                // First confirmation: the standard transaction/signing popup.
                bool confirmed = await ShowDefaultAsync(request, ct);

                // Second confirmation: a scene transaction with a resolved recipient must clear the
                // recipient gate, shown only after the user confirms the first popup.
                if (confirmed && request.Gate != RecipientGate.None)
                    confirmed = await recipientPopup.ShowAsync(request, ct);

                return confirmed;
            }
            finally { gameObject.SetActive(false); }
        }

        private async UniTask<bool> ShowDefaultAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            if (defaultContent != null) defaultContent.SetActive(true);

            UseConfig(request.IsTransaction ? transactionConfig : signingConfig);

            // Hide description and details panel for internal features (Gifting, Donations)
            // since they already display this information in their own UI
            description.gameObject.SetActive(!request.HideDescription);
            transactionInfoPanel.SetActive(request.IsTransaction && !request.HideDetailsPanel);

            if (request.IsTransaction && !request.HideDetailsPanel)
            {
                // string networkName = string.IsNullOrEmpty(request.NetworkName) ? "Ethereum Mainnet" : request.NetworkName!;
                string feeEth = string.IsNullOrEmpty(request.EstimatedGasFeeEth) ? "0.0" : request.EstimatedGasFeeEth!;
                string balanceEth = string.IsNullOrEmpty(request.BalanceEth) ? "0.0" : request.BalanceEth!;

                estimatedGasFeeValue.text = $"{feeEth} ETH";
                balanceValue.text = $"{balanceEth} ETH";
            }

            var tcs = new UniTaskCompletionSource<bool>();

            cancelButton.onClick.AddListener(OnCancel);
            continueButton.onClick.AddListener(OnContinue);

            try { return await tcs.Task.AttachExternalCancellation(ct); }
            finally
            {
                cancelButton.onClick.RemoveListener(OnCancel);
                continueButton.onClick.RemoveListener(OnContinue);

                // Hide the default content so only the recipient popup remains for the second step.
                if (defaultContent != null) defaultContent.SetActive(false);
            }

            void OnCancel() => tcs.TrySetResult(false);
            void OnContinue() => tcs.TrySetResult(true);
        }
    }
}
