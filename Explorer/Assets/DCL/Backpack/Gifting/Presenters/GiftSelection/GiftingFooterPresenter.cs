using System;
using DCL.Backpack.Gifting.Views;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingFooterPresenter : IDisposable
    {
        public event Action OnCancel;
        public event Action OnSendGift;

        private readonly GiftingFooterView view;

        private const string DEFAULT_INFO_MESSAGE = "Gifting an item cannot be undone.";
        private const string SELECTED_ITEM_INFO_MESSAGE_FORMAT = "You are about to send <b>{0}</b> to <b>{1}</b>";
        
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

        public void UpdateState(string? selectedItemName, string? recipient = null)
        {
            bool isItemSelected = !string.IsNullOrEmpty(selectedItemName);

            view.SendGiftButton.interactable = isItemSelected;
            view.InfoMessageContainer.SetActive(true);
            view.InfoMessageLabel.text = isItemSelected
                ? string.Format(SELECTED_ITEM_INFO_MESSAGE_FORMAT, selectedItemName, recipient)
                : DEFAULT_INFO_MESSAGE;
        }

        public void Dispose()
        {
            view.CancelButton.onClick.RemoveAllListeners();
            view.SendGiftButton.onClick.RemoveAllListeners();
        }
    }
}