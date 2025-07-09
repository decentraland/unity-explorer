using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatView2 : ViewBase, IDisposable
    {
        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the chat window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float BackgroundFadeTime = 0.2f;

        [Tooltip("The time it waits, in seconds, since the scroll view reaches the bottom until the scroll-to-bottom button starts hiding.")]
        [SerializeField]
        private float scrollToBottomButtonTimeBeforeHiding = 2.0f;

        [Tooltip("The time it takes, in seconds, for the scroll-to-bottom button to fade out.")]
        [SerializeField]
        private float scrollToBottomButtonFadeOutDuration = 0.5f;

        [Tooltip("The icon to use for the Nearby conversation.")]
        [SerializeField]
        private Sprite nearbyConversationIcon;

        [Header("UI elements")]
        [SerializeField]
        private ChatInputBoxElement chatInputBox;

        [SerializeField]
        private ChatInputBoxMaskElement inputBoxMask;

        [SerializeField]
        private Image unfoldedPanelInteractableArea;

        [Header("Messages")]

        [SerializeField]
        private ChatMessageViewerElement chatMessageViewer;

        [SerializeField]
        private CanvasGroup messagesPanelBackgroundCanvasGroup;

        [SerializeField]
        private GameObject messagesPanel;

        [SerializeField]
        private GameObject chatAndConversationsPanel;

        [SerializeField]
        private ChatMemberListView memberListView;

        [Header("Title bar")]

        [SerializeField]
        private ChatTitleBarView chatTitleBar;

        [SerializeField]
        private CanvasGroup titlebarCanvasGroup;

        [Header("Scroll to bottom")]

        [SerializeField]
        private Button scrollToBottomButton;

        [SerializeField]
        private TMP_Text scrollToBottomNumberText;

        [SerializeField]
        private CanvasGroup scrollToBottomCanvasGroup;

        [Header("Conversations toolbar")]

        [SerializeField]
        private ChatConversationsToolbarView conversationsToolbar;

        [SerializeField]
        private CanvasGroup conversationsToolbarCanvasGroup;

        public void Dispose()
        {
            
        }
    }
}
