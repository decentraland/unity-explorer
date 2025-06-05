using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void VisibilityChangedDelegate(bool isVisible);
        public delegate void DeleteChatHistoryRequestedDelegate();

        public event Action? CloseChatButtonClicked;
        public event Action? CloseMemberListButtonClicked;
        public event Action? HideMemberListButtonClicked;
        public event Action? ShowMemberListButtonClicked;

        public event VisibilityChangedDelegate? ContextMenuVisibilityChanged;
        public event DeleteChatHistoryRequestedDelegate? DeleteChatHistoryRequested;

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
        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private bool isInitialized;

        /// <summary>
        /// Gets the button that is currently available for folding the chat panel. The titlebar may change depending on whether the Member List is visible or not.
        /// </summary>
        public Button CurrentTitleBarCloseButton
        {
            get
            {
                if (closeChatButton.gameObject.activeInHierarchy)
                    return closeChatButton;
                else
                    return closeMemberListButton;
            }
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            profileView.InjectDependencies(dependencies);
        }

        public void Initialize()
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

        public void SetupProfileView(Web3Address userId, ProfileRepositoryWrapper profileDataProvider)
        {
            cts = cts.SafeRestart();
            profileView.gameObject.SetActive(true);
            profileView.SetupAsync(userId, profileDataProvider, cts.Token).Forget();
            nearbyChannelContainer.SetActive(false);
            memberCountObject.SetActive(false);
        }

        private void OnOpenContextMenuButtonClicked()
        {
            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            openContextMenuButton.OnSelect(null);
            ContextMenuVisibilityChanged?.Invoke(true);

            viewDependencies.GlobalUIViews.ShowChatContextMenuAsync(openContextMenuButton.transform.position, chatOptionsContextMenuData, OnDeleteChatHistoryButtonClicked, OnContextMenuClosed, contextMenuTask.Task).Forget();
        }

        private void OnDeleteChatHistoryButtonClicked()
        {
            DeleteChatHistoryRequested?.Invoke();
        }

        private void OnContextMenuClosed()
        {
            ContextMenuVisibilityChanged?.Invoke(false);
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
