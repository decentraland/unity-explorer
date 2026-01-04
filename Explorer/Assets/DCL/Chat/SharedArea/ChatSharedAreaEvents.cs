namespace DCL.ChatArea
{
    public static class ChatSharedAreaEvents
    {
        public struct PointerEnterChatPanelEvent { }
        public struct PointerExitChatPanelEvent { }
        public struct FocusChatPanelEvent { }
        public struct ToggleChatPanelEvent { }
        public struct ChatPanelViewShowEvent { }
        public struct FullscreenViewOpenEvent { }
        public struct FullscreenClosedEvent { }
        public struct UISubmitPerformedEvent { }

        public struct ChatPanelVisibilityStateChangedEvent
        {
            public bool IsVisible { get; }

            public ChatPanelVisibilityStateChangedEvent(bool isVisible)
            {
                IsVisible = isVisible;
            }
        }
    }
}
