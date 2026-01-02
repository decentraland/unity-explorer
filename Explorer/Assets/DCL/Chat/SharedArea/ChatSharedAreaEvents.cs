namespace DCL.ChatArea
{
    public static class ChatSharedAreaEvents
    {
        public struct ChatPanelPointerEnterEvent { }
        public struct ChatPanelPointerExitEvent { }
        public struct FocusChatPanelEvent { }
        public struct ToggleChatPanelEvent { }
        public struct ChatPanelViewShowEvent { }
        public struct FullscreenViewOpenEvent { }
        public struct FullscreenClosedEvent { }

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
