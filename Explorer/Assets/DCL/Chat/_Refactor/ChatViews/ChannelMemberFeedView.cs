using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.UI.Utilities;
using DCL.Web3;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public class ChannelMemberFeedView : MonoBehaviour
    {
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private GameObject loadingSpinner;

        // This list is shared by reference between the view and the presenter
        private IReadOnlyList<ChatMemberListViewModel> membersToDisplay = Array.Empty<ChatMemberListViewModel>();

        private void Awake()
        {
            loopListView.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            loopListView.InitListView(0, OnGetItemByIndex);
        }

        public event Action<UserProfileMenuRequest>? OnMemberContextMenuRequested;
        public event Action<string> OnMemberItemRequested;

        public void SetLoading(bool isLoading)
        {
            //loadingSpinner?.SetActive(isLoading);
            loopListView.gameObject.SetActive(true);
        }

        public void SetData(IReadOnlyList<ChatMemberListViewModel> memberList)
        {
            membersToDisplay = memberList;

            loopListView.SetListItemCount(membersToDisplay.Count);
            loopListView.RefreshAllShownItem();
            SetLoading(false);
        }

        public void UpdateMember(int index)
        {
            LoopListViewItem2? item = loopListView.GetShownItemByItemIndex(index);

            if (item == null) return;

            ChannelMemberEntryView? itemComponent = item.GetComponent<ChannelMemberEntryView>();
            itemComponent.Setup(membersToDisplay[index]);
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= membersToDisplay.Count)
                return null;

            ChatMemberListViewModel? model = membersToDisplay[index];
            LoopListViewItem2 newItem = listView.NewListViewItem("ChatMemberListItem2");

            ChannelMemberEntryView? itemComponent = newItem.GetComponent<ChannelMemberEntryView>();
            itemComponent.Setup(model);

            itemComponent.OnContextMenuRequested -= HandleItemContextMenuRequest;
            itemComponent.OnContextMenuRequested += HandleItemContextMenuRequest;
            
            itemComponent.OnItemSelectRequested -= HandleItemSelectedRequest;
            itemComponent.OnItemSelectRequested += HandleItemSelectedRequest;
            
            

            return newItem;
        }

        private void HandleItemContextMenuRequest(MemberEntryContextMenuRequest request)
        {
            var data = new UserProfileMenuRequest
            {
                WalletAddress = new Web3Address(request.UserId), Position = request.Position, AnchorPoint = MenuAnchorPoint.TOP_RIGHT, Offset = Vector2.zero
            };

            OnMemberContextMenuRequested?.Invoke(data);
        }
        
        private void HandleItemSelectedRequest(MemberEntryContextMenuRequest request)
        {
            OnMemberItemRequested?.Invoke(request.UserId);
        }

        public void Show() =>
            gameObject.SetActive(true);

        public void Hide() =>
            gameObject.SetActive(false);
    }
}
