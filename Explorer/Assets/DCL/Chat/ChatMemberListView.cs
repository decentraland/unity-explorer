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
        /// <summary>
        /// A subset of a Profile, stores only the necessary data to be presented by the view.
        /// </summary>
        public struct MemberData
        {
            public string Id;
            public string Name;
            public string FaceSnapshotUrl;
            public string WalletId;
            public ChatMemberConnectionStatus ConnectionStatus;
            public Color ProfileColor;
        }

        public delegate void VisibilityChangedDelegate(bool isVisible);

        /// <summary>
        /// Raised whenever the visibility of the member list changes.
        /// </summary>
        public event VisibilityChangedDelegate VisibilityChanged;

        [SerializeField]
        private LoopListView2 loopListView;

        private List<MemberData> members = new ();

        private ViewDependencies viewDependencies;
        private bool isInitialized;
        private bool isVisible;

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
                    VisibilityChanged?.Invoke(value);
                }
            }
        }

        private void Awake()
        {
            loopListView.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        /// <summary>
        /// Replaces the data to be represented by the view.
        /// </summary>
        /// <param name="memberData">The data related to the members of the list.</param>
        public void SetData(List<MemberData> memberData)
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
            memberItem.Id = members[index].Id;
            memberItem.Name = members[index].Name;
            memberItem.SetupProfilePicture(viewDependencies, members[index].ProfileColor, members[index].FaceSnapshotUrl, members[index].Id);
            memberItem.ConnectionStatus = members[index].ConnectionStatus;
            memberItem.Tag = members[index].WalletId;
            memberItem.NameTextColor = members[index].ProfileColor;
            memberItem.ContextMenuButtonClicked -= OnContextMenuButtonClickedAsync;
            memberItem.ContextMenuButtonClicked += OnContextMenuButtonClickedAsync;

            return newItem;
        }

        private async void OnContextMenuButtonClickedAsync(ChatMemberListViewItem listItem, Transform buttonPosition, Action onMenuHide)
        {
            contextMenuCts = contextMenuCts.SafeRestart();
            await viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(listItem.Id), buttonPosition.position, contextMenuCts.Token, onMenuHide);
        }
    }
}
