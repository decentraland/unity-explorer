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
            view.SendGiftButton.interactable = false;
            view.InfoMessageContainer.SetActive(false);
        }

        public void SetInfoMessage(string message)
        {
            view.InfoMessageLabel.text = message;
            view.InfoMessageContainer.SetActive(true);
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