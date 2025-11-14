using System;
using DCL.Backpack.Gifting.Views;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingFooterPresenter : IDisposable
    {
        public event Action OnCancel;
        public event Action OnSendGift;

        private readonly GiftingFooterView view;

        private const string DEFAULT_INFO_MESSAGE = "Select an item from your inventory to give it as a gift.";
        
        public GiftingFooterPresenter(GiftingFooterView view)
        {
            this.view = view;

            view.CancelButton.onClick.AddListener(() => OnCancel?.Invoke());
            view.SendGiftButton.onClick.AddListener(() => OnSendGift?.Invoke());
        }

        public void SetInitialState()
        {
            UpdateState(null);
        }

        /// <summary>
        ///     Updates the entire footer state based on the selected item.
        /// </summary>
        /// <param name="selectedItemName">The name of the item, or null if nothing is selected.</param>
        public void UpdateState(string? selectedItemName)
        {
            bool isItemSelected = !string.IsNullOrEmpty(selectedItemName);

            // Enable/disable the send button
            view.SendGiftButton.interactable = isItemSelected;

            // Show/hide and set the info message text
            view.InfoMessageContainer.SetActive(true); // Let's always show it
            view.InfoMessageLabel.text = isItemSelected
                ? $"You are going to send {selectedItemName}"
                : DEFAULT_INFO_MESSAGE;
        }

        public void SetInfoMessage(string message)
        {
            view.SendGiftButton.interactable = false;

            view.InfoMessageLabel.text = DEFAULT_INFO_MESSAGE;
            view.InfoMessageContainer.SetActive(true); 
        }

        public void SetSendEnabled(bool enabled)
        {
            view.SendGiftButton.interactable = enabled;
        }

        public void SetSendButtonInteractable(bool isInteractable)
        {
            view.SendGiftButton.interactable = isInteractable;
        }

        public void Dispose()
        {
            view.CancelButton.onClick.RemoveAllListeners();
            view.SendGiftButton.onClick.RemoveAllListeners();
        }
    }
}