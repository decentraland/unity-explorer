using DCL.UI.ProfileElements;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void VisibilityChangedDelegate(bool isVisible);

        public Action CloseChatButtonClicked;
        public Action CloseMemberListButtonClicked;
        public Action HideMemberListButtonClicked;
        public Action ShowMemberListButtonClicked;
        public event VisibilityChangedDelegate? ChatBubblesVisibilityChanged;
        public event VisibilityChangedDelegate? ContextMenuVisibilityChanged;

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

        [Header("Context Menu Data")]
        [SerializeField] private ChatOptionsContextMenuData chatOptionsContextMenuData;


        private ViewDependencies viewDependencies;
        private bool chatBubblesVisibility;

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void Initialize(bool chatBubblesVisibility)
        {
            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            closeMemberListButton.onClick.AddListener(OnCloseMemberListButtonClicked);
            showMemberListButton.onClick.AddListener(OnShowMemberListButtonClicked);
            hideMemberListButton.onClick.AddListener(OnHideMemberListButtonClicked);
            openContextMenuButton.onClick.AddListener(OnOpenContextMenuButtonClicked);
            this.chatBubblesVisibility = chatBubblesVisibility;
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

        public void SetupProfilePictureView(Color userColor, string faceSnapshotUrl, string userId)
        {
            profilePictureView.gameObject.SetActive(true);
            profilePictureView.SetupWithDependencies(viewDependencies, userColor, faceSnapshotUrl, userId);
            nearbyChannelImage.SetActive(false);
        }

        public void SetToggleChatBubblesValue(bool value)
        {
            chatBubblesVisibility = value;
        }

        private void OnOpenContextMenuButtonClicked()
        {
            ContextMenuVisibilityChanged?.Invoke(true);
            viewDependencies.GlobalUIViews.ShowChatContextMenuAsync(chatBubblesVisibility, openContextMenuButton.transform.position, chatOptionsContextMenuData, OnToggleChatBubblesValueChanged, OnContextMenuClosed).Forget();
        }

        private void OnContextMenuClosed()
        {
            ContextMenuVisibilityChanged?.Invoke(false);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            chatBubblesVisibility = isToggled;
            ChatBubblesVisibilityChanged?.Invoke(isToggled);
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
