// --- In a new file: Chat/Views/ChatMemberListItemView.cs ---
using System;
using DCL.Chat.ChatViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChannelMemberEntryView : MonoBehaviour
    {
        public event Action<string> OnContextMenuRequested;

        [Header("UI References")]
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private ChatProfilePictureView profilePictureView;
        [SerializeField] private GameObject onlineIndicator;
        [SerializeField] private Button contextMenuButton;
        [SerializeField] private Image nameHighlightImage;

        private string currentUserId;

        private void Awake()
        {
            contextMenuButton.onClick.AddListener(() => OnContextMenuRequested?.Invoke(currentUserId));
        }

        public void Setup(ChatMemberListViewModel model)
        {
            currentUserId = model.UserId;
            userNameText.text = model.UserName;
            onlineIndicator.SetActive(model.IsOnline);
            profilePictureView.Setup(model.ProfilePicture, model.IsLoading);
            // nameHighlightImage.color = model.ProfileColor;
        }
    }
}