using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [Serializable]
    public class Web3ConfirmationPopupConfig
    {
        public string Title;
        public string Description;
        public string ConfirmButtonText;
    }

    public class Web3ConfirmationView : MonoBehaviour
    {
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

        [SerializeField] private Web3ConfirmationPopupConfig transactionConfig;
        [SerializeField] private Web3ConfirmationPopupConfig signingConfig;

        private bool isTransaction;

        private void UseConfig(Web3ConfirmationPopupConfig config)
        {
            title.text = config.Title;
            description.text = config.Description;
            continueButtonText.text = config.ConfirmButtonText;
        }

        public UniTask<bool> ShowAsync(TransactionConfirmationRequest request)
        {
            string method = request.Method;

            isTransaction = string.Equals(method, "eth_sendTransaction", StringComparison.OrdinalIgnoreCase);

            UseConfig(isTransaction ? transactionConfig : signingConfig);
            transactionInfoPanel.SetActive(isTransaction);

            if (isTransaction)
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
                Cleanup();
                tcs.TrySetResult(true);
            }

            void Cleanup()
            {
                cancelButton.onClick.RemoveListener(OnCancel);
                continueButton.onClick.RemoveListener(OnContinue);
                gameObject.SetActive(false);
            }
        }
    }
}
