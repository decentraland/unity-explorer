using MVC;

namespace DCL.ChatArea
{
    public static class ChatSharedAreaEvents
    {
        public struct PointerEnterChatPanelEvent { }
        public struct PointerExitChatPanelEvent { }
        public struct FocusChatPanelEvent { }
        public struct ToggleChatPanelEvent { }
        public struct ChatPanelViewShowEvent { }

        public readonly struct MVCViewOpenEvent
        {
            public readonly CanvasOrdering.SortingLayer ViewSortingLayer;

            public MVCViewOpenEvent(CanvasOrdering.SortingLayer viewSortingLayer)
            {
                ViewSortingLayer = viewSortingLayer;
            }
        }

        public readonly struct MVCViewClosedEvent
        {
            public readonly CanvasOrdering.SortingLayer ViewSortingLayer;

            public MVCViewClosedEvent(CanvasOrdering.SortingLayer viewSortingLayer)
            {
                ViewSortingLayer = viewSortingLayer;
            }
        }
        public struct UISubmitPerformedEvent { }

        public readonly struct ChatPanelVisibilityStateChangedEvent
        {
            public bool IsVisible { get; }

            public ChatPanelVisibilityStateChangedEvent(bool isVisible)
            {
                IsVisible = isVisible;
            }
        }
    }
}
