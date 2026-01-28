using System;
using DCL.Backpack.Gifting.Views;
using DCL.Web3.Authenticators;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingFooterPresenter : IDisposable
    {
        public event Action OnCancel;
        public event Action OnSendGift;

        private readonly GiftingFooterView view;
        private readonly ICompositeWeb3Provider web3Provider;

        /// <summary>
        ///     Tracks whether this is the first click on the Send Gift button.
        ///     For ThirdWeb provider, first click shows tooltip, second click confirms.
        /// </summary>
        private bool isFirstClick = true;

        public GiftingFooterPresenter(GiftingFooterView view, ICompositeWeb3Provider web3Provider)
        {
            this.view = view;
            this.web3Provider = web3Provider;

            view.CancelButton.onClick.AddListener(HandleCancel);
            view.SendGiftButton.onClick.AddListener(HandleSendGiftClick);
        }

        public void SetInitialState()
        {
            ResetConfirmationState();
            UpdateState(null);
        }

        public void UpdateState(string? selectedItemName, string? recipient = null)
        {
            bool isItemSelected = !string.IsNullOrEmpty(selectedItemName);

            view.SendGiftButton.interactable = isItemSelected;
            view.InfoMessageContainer.SetActive(true);
            view.InfoMessageLabel.text = isItemSelected
                ? string.Format(GiftingTextIds.SelectedItemInfoMessageFormat, selectedItemName, recipient)
                : GiftingTextIds.DefaultInfoMessage;

            // Reset confirmation state when selection changes
            ResetConfirmationState();
        }

        private void HandleCancel()
        {
            ResetConfirmationState();
            OnCancel?.Invoke();
        }

        private void HandleSendGiftClick()
        {
            OnSendGift?.Invoke();
            return;

            // For ThirdWeb (OTP) provider, require double confirmation
            // For Dapp wallet, no double confirmation needed (browser handles it)
            if (web3Provider.IsThirdWebOTP && isFirstClick)
            {
                // First click: show tooltip warning
                view.ConfirmationTooltip.SetActive(true);
                isFirstClick = false;
            }
            else
            {
                // Second click (or non-ThirdWeb): proceed with send
                ResetConfirmationState();
                OnSendGift?.Invoke();
            }
        }

        private void ResetConfirmationState()
        {
            isFirstClick = true;
            view.ConfirmationTooltip.SetActive(false);
        }

        public void Dispose()
        {
            view.CancelButton.onClick.RemoveAllListeners();
            view.SendGiftButton.onClick.RemoveAllListeners();
        }
    }
}
