namespace DCL.ChatArea
{
    public static class ChatCoordinationEvents
    {
        public struct ChatPanelPointerEnterEvent { }
        public struct ChatPanelPointerExitEvent { }
        public struct ChatPanelPointerClickEvent { }
        public struct ChatPanelFocusEvent { }

        public struct ChatPanelVisibilityEvent
        {
            public bool IsVisible { get; }

            public ChatPanelVisibilityEvent(bool isVisible)
            {
                IsVisible = isVisible;
            }
        }

        public struct ChatPanelToggleEvent { }
        public struct ChatPanelViewShowEvent { }

        public struct ChatPanelShownInSharedSpaceEvent
        {
            public bool Focus { get; }

            public ChatPanelShownInSharedSpaceEvent(bool focus)
            {
                Focus = focus;
            }
        }

        public struct ChatPanelHiddenInSharedSpaceEvent { }
        public struct ChatPanelMvcViewShowedEvent { }
        public struct ChatPanelMvcViewClosedEvent { }
    }
}
