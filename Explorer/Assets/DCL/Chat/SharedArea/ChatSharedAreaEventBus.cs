using System;
using Utility;

namespace DCL.ChatArea
{
    public class ChatSharedAreaEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);

        public void RaisePointerEnter() =>
            Publish(new ChatSharedAreaEvents.ChatPanelPointerEnterEvent());

        public void RaisePointerExit() =>
            Publish(new ChatSharedAreaEvents.ChatPanelPointerExitEvent());

        public void RaiseFocusEvent() =>
            Publish(new ChatSharedAreaEvents.FocusChatPanelEvent());

        public void RaiseVisibilityEvent(bool isVisible) =>
            Publish(new ChatSharedAreaEvents.SetChatPanelVisibilityEvent(isVisible));

        public void RaiseToggleEvent() =>
            Publish(new ChatSharedAreaEvents.ToggleChatPanelEvent());

        public void RaiseViewShowEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelViewShowEvent());

        public void RaiseShownInSharedSpaceEvent(bool focus) =>
            Publish(new ChatSharedAreaEvents.ShowChatPanelEvent(focus));

        public void RaiseHiddenInSharedSpaceEvent() =>
            Publish(new ChatSharedAreaEvents.HideChatPanelEvent());

        public void RaiseFullscreenOpenEvent() =>
            Publish(new ChatSharedAreaEvents.FullscreenViewOpenEvent());

        public void RaiseFullscreenClosedEvent() =>
            Publish(new ChatSharedAreaEvents.FullscreenClosedEvent());

        public void RaiseVisibilityStateChangedEvent(bool isVisibleInSharedSpace) =>
            Publish(new ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent(isVisibleInSharedSpace));
    }
}
