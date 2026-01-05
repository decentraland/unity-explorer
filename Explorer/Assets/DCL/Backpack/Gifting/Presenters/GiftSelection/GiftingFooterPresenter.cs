using System;
using DCL.Backpack.Gifting.Views;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingFooterPresenter : IDisposable
    {
        public event Action OnCancel;
        public event Action OnSendGift;

        private readonly GiftingFooterView view;
        
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
                ? string.Format(GiftingTextIds.SelectedItemInfoMessageFormat, selectedItemName, recipient)
                : GiftingTextIds.DefaultInfoMessage;
        }

        public void Dispose()
        {
            view.CancelButton.onClick.RemoveAllListeners();
            view.SendGiftButton.onClick.RemoveAllListeners();
        }
    }
}