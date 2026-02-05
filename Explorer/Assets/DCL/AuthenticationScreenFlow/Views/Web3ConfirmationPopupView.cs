using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using MVC;
using System;
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
        [SerializeField] private GameObject tooltip;

        [Header("TRANSACTION")]
        [SerializeField] private GameObject transactionInfoPanel;
        [Space]
        [SerializeField] private TMP_Text balanceValue;
        [SerializeField] private TMP_Text costValue;
        [SerializeField] private TMP_Text estimatedGasFeeValue;

        [Space]
        [SerializeField] private Web3ConfirmationPopupConfig transactionConfig;
        [SerializeField] private Web3ConfirmationPopupConfig signingConfig;

        private bool isTransaction;
        private bool isFirstClick = true;

        private void OnEnable()
        {
            tooltip.SetActive(false);
            isFirstClick = true;
        }

        private void UseConfig(Web3ConfirmationPopupConfig config)
        {
            title.text = config.Title;
            description.text = config.Description;
            continueButtonText.text = config.ConfirmButtonText;
        }

        public UniTask<bool> ShowAsync(TransactionConfirmationRequest request)
        {
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

            gameObject.SetActive(true);

            var tcs = new UniTaskCompletionSource<bool>();

            cancelButton.onClick.AddListener(OnCancel);
            continueButton.onClick.AddListener(OnContinue);

            return tcs.Task;

            void OnCancel()
            {
                Cleanup();
                tcs.TrySetResult(false);
            }

            void OnContinue()
            {
                if (isFirstClick)
                {
                    tooltip.SetActive(true);
                    isFirstClick = false;
                }
                else
                {
                    Cleanup();
                    tcs.TrySetResult(true);
                }
            }

            void Cleanup()
            {
                cancelButton.onClick.RemoveListener(OnCancel);
                continueButton.onClick.RemoveListener(OnContinue);
                gameObject.SetActive(false);
                tooltip.SetActive(false);
                isFirstClick = true;
            }
        }
    }
}
