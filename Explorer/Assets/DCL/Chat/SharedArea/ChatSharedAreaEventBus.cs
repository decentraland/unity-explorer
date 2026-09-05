using MVC;
using System;
using Utility;

namespace DCL.ChatArea
{
    public class ChatSharedAreaEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);
        public void RaisePointerEnter() => Publish(new ChatSharedAreaEvents.PointerEnterChatPanelEvent());
        public void RaisePointerExit() => Publish(new ChatSharedAreaEvents.PointerExitChatPanelEvent());
        public void RaiseFocusEvent() => Publish(new ChatSharedAreaEvents.FocusChatPanelEvent());
        public void RaiseToggleEvent() => Publish(new ChatSharedAreaEvents.ToggleChatPanelEvent());
        public void RaiseViewShowEvent() => Publish(new ChatSharedAreaEvents.ChatPanelViewShowEvent());
        public void RaiseUISubmitEvent() => Publish(new ChatSharedAreaEvents.UISubmitPerformedEvent());
        public void RaiseMVCViewOpenEvent(CanvasOrdering.SortingLayer viewSortingLayer) =>
            Publish(new ChatSharedAreaEvents.MVCViewOpenEvent(viewSortingLayer));
        public void RaiseMVCViewClosedEvent(CanvasOrdering.SortingLayer viewSortingLayer) =>
            Publish(new ChatSharedAreaEvents.MVCViewClosedEvent(viewSortingLayer));
        public void RaiseVisibilityStateChangedEvent(bool isVisible) =>
            Publish(new ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent(isVisible));
    }
}
