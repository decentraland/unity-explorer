using Cysharp.Threading.Tasks;
using DCL.UI.ProfileElements;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void VisibilityChangedDelegate(bool isVisible);

        public event Action? CloseChatButtonClicked;
        public event Action? CloseMemberListButtonClicked;
        public event Action? HideMemberListButtonClicked;
        public event Action? ShowMemberListButtonClicked;
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

        [SerializeField] private GameObject memberCountObject;
        [SerializeField] private GameObject nearbyChannelContainer;
        [SerializeField] private SimpleProfileView profileView;

        [Header("Context Menu Data")]
        [SerializeField] private ChatOptionsContextMenuData chatOptionsContextMenuData;


        private ViewDependencies viewDependencies;
        private bool chatBubblesVisibility;
        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private bool isInitialized;

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            profileView.InjectDependencies(dependencies);
        }

        public void Initialize(bool chatBubblesVisibility)
        {
            if(isInitialized)
                return;

            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            closeMemberListButton.onClick.AddListener(OnCloseMemberListButtonClicked);
            showMemberListButton.onClick.AddListener(OnShowMemberListButtonClicked);
            hideMemberListButton.onClick.AddListener(OnHideMemberListButtonClicked);
            openContextMenuButton.onClick.AddListener(OnOpenContextMenuButtonClicked);
            profileView.ProfileContextMenuOpened += OnProfileContextMenuOpened;
            profileView.ProfileContextMenuClosed += OnProfileContextMenuClosed;
            this.chatBubblesVisibility = chatBubblesVisibility;
            isInitialized = true;
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
            nearbyChannelContainer.SetActive(true);
            memberCountObject.SetActive(true);
            profileView.gameObject.SetActive(false);
        }

        public void SetupProfileView(Web3Address userId)
        {
            cts = cts.SafeRestart();
            profileView.gameObject.SetActive(true);
            profileView.SetupAsync(userId, cts.Token).Forget();
            nearbyChannelContainer.SetActive(false);
            memberCountObject.SetActive(false);
        }

        public void SetToggleChatBubblesValue(bool value)
        {
            chatBubblesVisibility = value;
        }

        private void OnOpenContextMenuButtonClicked()
        {
            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            openContextMenuButton.OnSelect(null);
            ContextMenuVisibilityChanged?.Invoke(true);
            viewDependencies.GlobalUIViews.ShowChatContextMenuAsync(chatBubblesVisibility, openContextMenuButton.transform.position, chatOptionsContextMenuData, OnToggleChatBubblesValueChanged, OnContextMenuClosed, contextMenuTask.Task).Forget();
        }

        private void OnContextMenuClosed()
        {
            openContextMenuButton.OnDeselect(null);
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

        private void OnProfileContextMenuClosed()
        {
            ContextMenuVisibilityChanged?.Invoke(false);
        }

        private void OnProfileContextMenuOpened()
        {
            ContextMenuVisibilityChanged?.Invoke(true);
        }

        private void OnDisable()
        {
            contextMenuTask.TrySetResult();
        }
    }
}
