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
            Publish(new ChatSharedAreaEvents.ChatPanelFocusEvent());

        public void RaiseVisibilityEvent(bool isVisible) =>
            Publish(new ChatSharedAreaEvents.ChatPanelVisibilityEvent(isVisible));

        public void RaiseToggleEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelToggleEvent());

        public void RaiseViewShowEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelViewShowEvent());

        public void RaiseShownInSharedSpaceEvent(bool focus) =>
            Publish(new ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent(focus));

        public void RaiseHiddenInSharedSpaceEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent());

        public void RaiseMvcViewShowedEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelMvcViewShowedEvent());

        public void RaiseMvcViewClosedEvent() =>
            Publish(new ChatSharedAreaEvents.ChatPanelMvcViewClosedEvent());

        public void RaiseClickInsideEvent(System.Collections.Generic.IReadOnlyList<UnityEngine.EventSystems.RaycastResult> raycastResults) =>
            Publish(new ChatSharedAreaEvents.ChatPanelClickInsideEvent(raycastResults));

        public void RaiseClickOutsideEvent(System.Collections.Generic.IReadOnlyList<UnityEngine.EventSystems.RaycastResult> raycastResults) =>
            Publish(new ChatSharedAreaEvents.ChatPanelClickOutsideEvent(raycastResults));
    }
}
