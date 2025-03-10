using DCL.UI.ProfileElements;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour
    {
        public delegate void ChatBubbleVisibilityChangedDelegate(bool isVisible);

        public Action CloseChatButtonClicked;
        public Action CloseMemberListButtonClicked;
        public Action ShowMemberListButtonClicked;
        public Action HideMemberListButtonClicked;
        public event ChatBubbleVisibilityChangedDelegate? ChatBubbleVisibilityChanged;

        [SerializeField] private Button closeChatButton;
        [SerializeField] private Button closeMemberListButton;
        [SerializeField] private Button showMemberListButton;
        [SerializeField] private Button hideMemberListButton;
        [SerializeField] private Button openContextMenuButton;

        [SerializeField] private TMP_Text chatTitleMemberListNumberText;
        [SerializeField] private TMP_Text memberListTitleMemberListNumberText;
        [SerializeField] private TMP_Text chatChannelNameNameText;
        [SerializeField] private TMP_Text memberListChannelNameText;

        [SerializeField] private GameObject defaultChatTitlebar;
        [SerializeField] private GameObject memberListTitlebar;

        [SerializeField] private GameObject nearbyChannelImage;
        [SerializeField] private ProfilePictureView profilePictureView;

        public void Initialize()
        {
            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            closeMemberListButton.onClick.AddListener(OnCloseMemberListButtonClicked);
            showMemberListButton.onClick.AddListener(OnShowMemberListButtonClicked);
            hideMemberListButton.onClick.AddListener(OnHideMemberListButtonClicked);
            openContextMenuButton.onClick.AddListener(OnOpenContextMenuButtonClicked);
        }

        private void OnOpenContextMenuButtonClicked()
        {
        }

        public void ChangeTitleBarVisibility(bool isMemberListVisible)
        {
            defaultChatTitlebar.SetActive(!isMemberListVisible);
            memberListTitlebar.SetActive(isMemberListVisible);
        }

        public void SetMemberListNumberText(string userAmount)
        {
            chatTitleMemberListNumberText.text = userAmount;
            memberListTitleMemberListNumberText.text = userAmount;
        }

        public void SetChannelNameText(string channelName)
        {
            chatChannelNameNameText.text = channelName;
            memberListChannelNameText.text = channelName;
        }

        public void SetNearbyChannelImage()
        {
            nearbyChannelImage.SetActive(true);
            profilePictureView.gameObject.SetActive(false);
        }

        public void SetupProfilePictureView(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            profilePictureView.gameObject.SetActive(true);
            profilePictureView.SetupWithDependencies(dependencies, userColor, faceSnapshotUrl, userId);
            nearbyChannelImage.SetActive(false);
        }

        public void SetToggleChatBubblesValue(bool value)
        {
            /*get => chatBubblesToggle.Toggle.interactable;

          set
          {
              if (chatBubblesToggle.Toggle.interactable != value)
              {
                  chatBubblesToggle.Toggle.interactable = value;

                  chatBubblesToggle.IsSoundEnabled = false;
                  chatBubblesToggle.Toggle.isOn = chatBubblesToggle.Toggle.interactable;
                  chatBubblesToggle.IsSoundEnabled = true;
              }
          }*/
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        private void OnCloseMemberListButtonClicked()
        {
            CloseMemberListButtonClicked?.Invoke();
        }

        private void OnHideMemberListButtonClicked()
        {
            HideMemberListButtonClicked?.Invoke();
        }

        private void OnShowMemberListButtonClicked()
        {
            ShowMemberListButtonClicked?.Invoke();
        }

        private void OnCloseChatButtonClicked()
        {
            CloseChatButtonClicked?.Invoke();
        }
    }
}
