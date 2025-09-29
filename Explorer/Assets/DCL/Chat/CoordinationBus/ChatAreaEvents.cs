using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace DCL.ChatArea
{
    public static class ChatAreaEvents
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

        public struct ChatPanelClickInsideEvent
        {
            public IReadOnlyList<RaycastResult> RaycastResults { get; }

            public ChatPanelClickInsideEvent(IReadOnlyList<RaycastResult> raycastResults)
            {
                RaycastResults = raycastResults;
            }
        }
        public struct ChatPanelClickOutsideEvent
        {
            public IReadOnlyList<RaycastResult> RaycastResults { get; }

            public ChatPanelClickOutsideEvent(IReadOnlyList<RaycastResult> raycastResults)
            {
                RaycastResults = raycastResults;
            }
        }
    }
}
