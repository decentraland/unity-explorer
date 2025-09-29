using System;
using Utility;

namespace DCL.ChatArea
{
    public class ChatAreaEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);

        public void RaisePointerEnter() =>
            Publish(new ChatAreaEvents.ChatPanelPointerEnterEvent());

        public void RaisePointerExit() =>
            Publish(new ChatAreaEvents.ChatPanelPointerExitEvent());

        public void RaiseFocusEvent() =>
            Publish(new ChatAreaEvents.ChatPanelFocusEvent());

        public void RaiseVisibilityEvent(bool isVisible) =>
            Publish(new ChatAreaEvents.ChatPanelVisibilityEvent(isVisible));

        public void RaiseToggleEvent() =>
            Publish(new ChatAreaEvents.ChatPanelToggleEvent());

        public void RaiseViewShowEvent() =>
            Publish(new ChatAreaEvents.ChatPanelViewShowEvent());

        public void RaiseShownInSharedSpaceEvent(bool focus) =>
            Publish(new ChatAreaEvents.ChatPanelShownInSharedSpaceEvent(focus));

        public void RaiseHiddenInSharedSpaceEvent() =>
            Publish(new ChatAreaEvents.ChatPanelHiddenInSharedSpaceEvent());

        public void RaiseMvcViewShowedEvent() =>
            Publish(new ChatAreaEvents.ChatPanelMvcViewShowedEvent());

        public void RaiseMvcViewClosedEvent() =>
            Publish(new ChatAreaEvents.ChatPanelMvcViewClosedEvent());

        public void RaiseClickInsideEvent(System.Collections.Generic.IReadOnlyList<UnityEngine.EventSystems.RaycastResult> raycastResults) =>
            Publish(new ChatAreaEvents.ChatPanelClickInsideEvent(raycastResults));

        public void RaiseClickOutsideEvent(System.Collections.Generic.IReadOnlyList<UnityEngine.EventSystems.RaycastResult> raycastResults) =>
            Publish(new ChatAreaEvents.ChatPanelClickOutsideEvent(raycastResults));
    }
}
