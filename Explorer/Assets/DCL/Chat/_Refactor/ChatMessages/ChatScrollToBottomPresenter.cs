using DCL.Chat.History;
using DCL.Chat.ChatServices;
using System;

namespace DCL.Chat.ChatMessages
{
    public class ChatScrollToBottomPresenter : IDisposable
    {
        private readonly ChatScrollToBottomView view;
        private readonly CurrentChannelService currentChannelService;

        private bool isFocused;
        private bool shouldBeVisible;

        public event Action RequestScrollAction;

        public ChatScrollToBottomPresenter(ChatScrollToBottomView view,
            CurrentChannelService currentChannelService)
        {
            this.view = view;
            this.currentChannelService = currentChannelService;
            view.OnClicked += () => RequestScrollAction?.Invoke();
        }

        public void OnFocusChanged(bool isFocused)
        {
            this.isFocused = isFocused;
            EvaluateAndApplyVisibility(true);
        }

        public void OnMessageReceived(bool isOwnMessage, bool wasAtBottom)
        {
            shouldBeVisible = !isOwnMessage && !wasAtBottom;
            EvaluateAndApplyVisibility(false);
        }

        public void OnScrolledToBottom()
        {
            shouldBeVisible = false;
            view.SetVisibility(false, 0, true);
        }

        public void OnChannelChanged()
        {
            shouldBeVisible = false;
            view.SetState(false, 0); // Hide instantly
        }

        private void EvaluateAndApplyVisibility(bool useAnimation)
        {
            bool shouldShowNow = shouldBeVisible && isFocused;

            if (shouldShowNow)
            {
                var channel = currentChannelService.CurrentChannel;
                if (channel == null) return;

                int unreadCount = channel.Messages.Count - channel.ReadMessages;

                if (unreadCount > 0)
                    view.SetState(true, unreadCount);
                else
                    view.SetState(false, 0);
            }
            else
            {
                view.SetState(false, 0);
            }
        }

        public void Dispose()
        {
            view.OnClicked -= () => RequestScrollAction?.Invoke();
        }
    }
}