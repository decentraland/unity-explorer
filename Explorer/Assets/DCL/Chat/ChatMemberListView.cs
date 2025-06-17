using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DCL.Web3;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatMemberListView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void VisibilityChangedDelegate(bool isVisible);

        /// <summary>
        /// Raised whenever the visibility of the member list changes.
        /// </summary>
        public event VisibilityChangedDelegate VisibilityChanged;

        [SerializeField]
        private LoopListView2 loopListView;

        private List<ChatUserData> members = new ();

        private ViewDependencies viewDependencies;
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private bool isInitialized;
        private bool isVisible;
        private UniTaskCompletionSource contextMenuTask = new ();
        private CancellationTokenSource contextMenuCts = new ();

        public bool IsVisible
        {
            get => isVisible;

            set
            {
                if (value != IsVisible)
                {
                    isVisible = value;
                    gameObject.SetActive(value);

                    if (!IsVisible)
                    {
                        loopListView.SetListItemCount(0);
                    }

                    VisibilityChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// Gets whether any of the context menus of any member of the list is open.
        /// </summary>
        public bool IsContextMenuOpen { get; private set; }

        private void Awake()
        {
            loopListView.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            profileRepositoryWrapper = profileDataProvider;
        }

        /// <summary>
        /// Replaces the data to be represented by the view.
        /// </summary>
        /// <param name="memberData">The data related to the members of the list.</param>
        public void SetData(List<ChatUserData> memberData)
        {
            members = memberData;

            if (!isInitialized)
            {
                loopListView.InitListView(members.Count, OnGetItemByIndex);
                isInitialized = true;
            }

            loopListView.SetListItemCount(members.Count);
            loopListView.RefreshAllShownItem();
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 list, int index)
        {
            if (index < 0 || index >= members.Count)
                return null;

            LoopListViewItem2 newItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            ChatMemberListViewItem memberItem = newItem.GetComponent<ChatMemberListViewItem>();
            memberItem.Id = members[index].WalletAddress;
            memberItem.Name = members[index].Name;
            memberItem.SetupProfilePicture(profileRepositoryWrapper, members[index].ProfileColor, members[index].FaceSnapshotUrl, members[index].WalletAddress);
            memberItem.ConnectionStatus = members[index].ConnectionStatus;
            memberItem.Tag = members[index].WalletId;
            memberItem.NameTextColor = members[index].ProfileColor;
            memberItem.ContextMenuButtonClicked -= OnContextMenuButtonClickedAsync;
            memberItem.ContextMenuButtonClicked += OnContextMenuButtonClickedAsync;

            return newItem;
        }

        private async void OnContextMenuButtonClickedAsync(ChatMemberListViewItem listItem, Transform buttonPosition, Action onMenuHide)
        {
            contextMenuTask?.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            contextMenuCts = contextMenuCts.SafeRestart();
            IsContextMenuOpen = true;
            await viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(listItem.Id), buttonPosition.position, default(Vector2), contextMenuCts.Token, contextMenuTask.Task, onMenuHide);
            IsContextMenuOpen = false;
        }

        private void OnDisable()
        {
            contextMenuTask?.TrySetResult();
        }
    }
}
