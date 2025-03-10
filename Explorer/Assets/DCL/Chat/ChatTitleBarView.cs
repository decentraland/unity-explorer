using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour
    {
        public Action CloseChatButtonClicked;
        public Action CloseMemberListButtonClicked;
        public Action ShowMemberListButtonClicked;
        public Action HideMemberListButtonClicked;

        [SerializeField] private Button closeChatButton;
        [SerializeField] private Button closeMemberListButton;
        [SerializeField] private Button showMemberListButton;
        [SerializeField] private Button hideMemberListButton;

        [SerializeField] private GameObject defaultChatTitlebar;
        [SerializeField] private GameObject memberListTitlebar;

        public void Initialize()
        {
            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            closeMemberListButton.onClick.AddListener(OnCloseMemberListButtonClicked);
            showMemberListButton.onClick.AddListener(OnShowMemberListButtonClicked);
            hideMemberListButton.onClick.AddListener(OnHideMemberListButtonClicked);
        }

        public void ChangeTitleBarVisibility(bool isMemberListVisible)
        {
            defaultChatTitlebar.SetActive(!isMemberListVisible);
            memberListTitlebar.SetActive(isMemberListVisible);
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
