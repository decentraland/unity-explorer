using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace DCL.ChatArea
{
    public static class ChatSharedAreaEvents
    {
        public struct ChatPanelPointerEnterEvent { }
        public struct ChatPanelPointerExitEvent { }
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

        public struct ChatPanelGlobalClickEvent
        {
            public IReadOnlyList<RaycastResult> RaycastResults { get; }

            public ChatPanelGlobalClickEvent(IReadOnlyList<RaycastResult> raycastResults)
            {
                RaycastResults = raycastResults;
            }
        }

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
