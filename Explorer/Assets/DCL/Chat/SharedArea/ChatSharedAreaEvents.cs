namespace DCL.ChatArea
{
    public static class ChatSharedAreaEvents
    {
        public struct ChatPanelPointerEnterEvent { }
        public struct ChatPanelPointerExitEvent { }
        public struct FocusChatPanelEvent { }

        public struct SetChatPanelVisibilityEvent
        {
            public bool IsVisible { get; }

            public SetChatPanelVisibilityEvent(bool isVisible)
            {
                IsVisible = isVisible;
            }
        }

        public struct ToggleChatPanelEvent { }
        public struct ChatPanelViewShowEvent { }

        public struct ShowChatPanelEvent
        {
            public bool Focus { get; }

            public ShowChatPanelEvent(bool focus)
            {
                Focus = focus;
            }
        }

        public struct HideChatPanelEvent { }
        public struct FullscreenViewOpenEvent { }
        public struct FullscreenClosedEvent { }

        public struct ChatPanelVisibilityStateChangedEvent
        {
            public bool IsVisibleInSharedSpace { get; }

            public ChatPanelVisibilityStateChangedEvent(bool isVisibleInSharedSpace)
            {
                IsVisibleInSharedSpace = isVisibleInSharedSpace;
            }
        }
    }
}
