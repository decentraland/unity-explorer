using System;
using DCL.Chat.ChatViewModels;
using DCL.UI.Utilities;
using SuperScrollView;
using System.Collections.Generic;
using DCL.Chat.Services;
using DCL.Web3;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChannelMemberFeedView : MonoBehaviour
    {
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private GameObject loadingSpinner;

        private List<ChatMemberListViewModel> membersToDisplay = new ();
        public event Action<UserProfileMenuRequest> OnMemberContextMenuRequested;

        private void Awake()
        {
            loopListView.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            loopListView.InitListView(0, OnGetItemByIndex);
        }
        
        public void SetLoading(bool isLoading)
        {
            //loadingSpinner?.SetActive(isLoading);
            loopListView.gameObject.SetActive(true);
        }

        public void SetData(List<ChatMemberListViewModel> memberList)
        {
            membersToDisplay = memberList;

            loopListView.SetListItemCount(membersToDisplay.Count);
            loopListView.RefreshAllShownItem();
            SetLoading(false);
        }

        public void UpdateMember(ChatMemberListViewModel updatedViewModel)
        {
            for (int i = 0; i < membersToDisplay.Count; i++)
            {
                if (membersToDisplay[i].UserId == updatedViewModel.UserId)
                {
                    var item = loopListView.GetShownItemByItemIndex(i);
                    if (item != null)
                    {
                        var itemComponent = item.GetComponent<ChannelMemberEntryView>();
                        itemComponent.Setup(updatedViewModel);
                    }

                    break;
                }
            }
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= membersToDisplay.Count)
                return null;

            var model = membersToDisplay[index];
            LoopListViewItem2 newItem = listView.NewListViewItem("ChatMemberListItem2");

            var itemComponent = newItem.GetComponent<ChannelMemberEntryView>();
            itemComponent.Setup(model);
            
            itemComponent.OnContextMenuRequested -= HandleItemContextMenuRequest;
            itemComponent.OnContextMenuRequested += HandleItemContextMenuRequest;

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

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}