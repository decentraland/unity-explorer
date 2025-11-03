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
            view.SendGiftButton.interactable = false;
            view.InfoMessageContainer.SetActive(false);
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