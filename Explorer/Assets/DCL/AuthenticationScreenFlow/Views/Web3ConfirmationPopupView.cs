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

        [Header("RECIPIENT CONFIRMATION")]
        [SerializeField] private TransactionRecipientPopupView recipientPopup;

        /// <summary>
        ///     Asks for confirmation of the transaction or signature, followed by a confirmation of the
        ///     recipient when <paramref name="recipientDescription" /> is given.
        /// </summary>
        public async UniTask<bool> ShowAsync(TransactionConfirmationRequest request, string? recipientDescription, CancellationToken ct)
        {
            gameObject.SetActive(true);

            try
            {
                if (!await ShowDefaultAsync(request, ct))
                    return false;

                if (recipientDescription == null)
                    return true;

                return await recipientPopup.ShowAsync(recipientDescription, ct);
            }
            finally { gameObject.SetActive(false); }
        }

        private async UniTask<bool> ShowDefaultAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            UseConfig(request.IsTransaction ? transactionConfig : signingConfig);

            // Hide description and details panel for internal features (Gifting, Donations)
            // since they already display this information in their own UI
            description.gameObject.SetActive(!request.HideDescription);

            bool showDetails = request.IsTransaction && !request.HideDetailsPanel;
            transactionInfoPanel.SetActive(showDetails);

            if (showDetails)
            {
                string feeEth = string.IsNullOrEmpty(request.EstimatedGasFeeEth) ? "0.0" : request.EstimatedGasFeeEth!;
                string balanceEth = string.IsNullOrEmpty(request.BalanceEth) ? "0.0" : request.BalanceEth!;

                estimatedGasFeeValue.text = $"{feeEth} ETH";
                balanceValue.text = $"{balanceEth} ETH";
            }

            int clickedIndex = await UniTask.WhenAny(continueButton.OnClickAsync(ct), cancelButton.OnClickAsync(ct));
            return clickedIndex == 0;
        }

        private void UseConfig(Web3ConfirmationPopupConfig config)
        {
            title.text = config.Title;
            description.text = config.Description;
            continueButtonText.text = config.ConfirmButtonText;
        }
    }
}
