using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class TransactionFeeConfirmationView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject Root { get; private set; } = null!;

        [field: Header("Texts")]
        [field: SerializeField]
        public TMP_Text Title { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text Description { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text EstimatedGasFeeValue { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text BalanceValue { get; private set; } = null!;

        [field: Header("Buttons")]
        [field: SerializeField]
        public Button CancelButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ContinueButton { get; private set; } = null!;

        public UniTask<bool> ShowAsync(
            string networkName,
            string estimatedGasFeeEth,
            string balanceEth)
        {
            Root.SetActive(true);

            Title.text = "Confirm Transaction";

            Description.text =
                $"You are about to perform a transaction on the {networkName} network. " +
                "Network transactions require gas, paid in the network's native currency.";

            EstimatedGasFeeValue.text = $"{estimatedGasFeeEth} ETH";
            BalanceValue.text = $"{balanceEth} ETH";

            var tcs = new UniTaskCompletionSource<bool>();

            CancelButton.onClick.AddListener(OnCancel);
            ContinueButton.onClick.AddListener(OnContinue);

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
                CancelButton.onClick.RemoveListener(OnCancel);
                ContinueButton.onClick.RemoveListener(OnContinue);
                Root.SetActive(false);
            }
        }
    }
}
