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
        public string ConfirmButtonText = null!;
        public string Title = null!;
        [Multiline]
        public string Description = null!;
    }

    public class Web3ConfirmationPopupView : ViewBase
    {
        [Space]
        [SerializeField] private TMP_Text title = null!;
        [SerializeField] private TMP_Text description = null!;

        [Space]
        [SerializeField] private Button cancelButton = null!;
        [SerializeField] private Button continueButton = null!;
        [SerializeField] private TMP_Text continueButtonText = null!;

        [Header("TRANSACTION")]
        [SerializeField] private GameObject transactionInfoPanel = null!;
        [Space]
        [SerializeField] private TMP_Text balanceValue = null!;
        [SerializeField] private TMP_Text costValue = null!;
        [SerializeField] private TMP_Text estimatedGasFeeValue = null!;

        [Header("RAW REQUEST")]
        [SerializeField] private GameObject rawRequestPanel = null!;
        [SerializeField] private ScrollRect rawRequestScroll = null!;
        [SerializeField] private TMP_Text rawRequestText = null!;

        [Space]
        [SerializeField] private Web3ConfirmationPopupConfig transactionConfig = null!;
        [SerializeField] private Web3ConfirmationPopupConfig signingConfig = null!;

        /// <summary>
        ///     Replaces the confirm label when a second step follows, so the button does not read as the
        ///     last word on a request the user has not finished reviewing.
        /// </summary>
        [SerializeField] private string nextStepButtonText = "CONTINUE";

        [Header("RECIPIENT CONFIRMATION")]
        [SerializeField] private TransactionRecipientPopupView recipientPopup = null!;

        /// <summary>
        ///     Asks for confirmation of the transaction or signature, followed by a confirmation of the
        ///     recipient when <paramref name="recipientDescription" /> is given. A
        ///     <paramref name="rawPayload" /> is the request rendered verbatim, shown for review when it
        ///     matches no shape the client can summarize.
        /// </summary>
        public async UniTask<bool> ShowAsync(TransactionConfirmationRequest request, string? recipientDescription, string? rawPayload, CancellationToken ct)
        {
            gameObject.SetActive(true);

            try
            {
                if (!await ShowDefaultAsync(request, rawPayload, recipientDescription != null, ct))
                    return false;

                if (recipientDescription == null)
                    return true;

                return await recipientPopup.ShowAsync(recipientDescription, ct);
            }
            finally { gameObject.SetActive(false); }
        }

        private async UniTask<bool> ShowDefaultAsync(TransactionConfirmationRequest request, string? rawPayload, bool hasNextStep, CancellationToken ct)
        {
            UseConfig(request.IsTransaction ? transactionConfig : signingConfig);

            if (hasNextStep)
                continueButtonText.text = nextStepButtonText;

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

            ShowRawPayload(rawPayload);

            // The button that was not clicked stays subscribed until its token is cancelled.
            using var clickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                int clickedIndex = await UniTask.WhenAny(continueButton.OnClickAsync(clickCts.Token), cancelButton.OnClickAsync(clickCts.Token));
                return clickedIndex == 0;
            }
            finally { clickCts.Cancel(); }
        }

        private void ShowRawPayload(string? rawPayload)
        {
            rawRequestPanel.SetActive(rawPayload != null);

            if (rawPayload == null)
                return;

            rawRequestText.text = rawPayload;

            // The panel keeps the offset it was left at, so every request starts from the top of its own text.
            rawRequestScroll.StopMovement();
            rawRequestScroll.content.anchoredPosition = Vector2.zero;
        }

        private void UseConfig(Web3ConfirmationPopupConfig config)
        {
            title.text = config.Title;
            description.text = config.Description;
            continueButtonText.text = config.ConfirmButtonText;
        }
    }
}
