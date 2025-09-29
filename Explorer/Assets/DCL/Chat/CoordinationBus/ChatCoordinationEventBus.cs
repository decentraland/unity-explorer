using System;
using Utility;

namespace DCL.ChatArea
{
    public class ChatCoordinationEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);

        public void RaisePointerEnter() =>
            Publish(new ChatCoordinationEvents.ChatPanelPointerEnterEvent());

        public void RaisePointerExit() =>
            Publish(new ChatCoordinationEvents.ChatPanelPointerExitEvent());

        public void RaisePointerClick() =>
            Publish(new ChatCoordinationEvents.ChatPanelPointerClickEvent());

        public void RaiseFocusEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelFocusEvent());

        public void RaiseVisibilityEvent(bool isVisible) =>
            Publish(new ChatCoordinationEvents.ChatPanelVisibilityEvent(isVisible));

        public void RaiseToggleEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelToggleEvent());

        public void RaiseViewShowEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelViewShowEvent());

        public void RaiseShownInSharedSpaceEvent(bool focus) =>
            Publish(new ChatCoordinationEvents.ChatPanelShownInSharedSpaceEvent(focus));

        public void RaiseHiddenInSharedSpaceEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelHiddenInSharedSpaceEvent());

        public void RaiseMvcViewShowedEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelMvcViewShowedEvent());

        public void RaiseMvcViewClosedEvent() =>
            Publish(new ChatCoordinationEvents.ChatPanelMvcViewClosedEvent());
    }
}
