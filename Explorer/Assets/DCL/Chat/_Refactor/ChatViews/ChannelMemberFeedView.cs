using DCL.Chat.ChatViewModels;
using DCL.UI.Utilities;
using SuperScrollView;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChannelMemberFeedView : MonoBehaviour
    {
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private GameObject loadingSpinner;

        private List<ChatMemberListViewModel> members = new ();
        public event System.Action<string> OnMemberContextMenuRequested;

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
            members = memberList;
            loopListView.SetListItemCount(members.Count, true);
            loopListView.RefreshAllShownItem();
            SetLoading(false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= members.Count)
                return null;

            ChatMemberListViewModel model = members[index];
            LoopListViewItem2 newItem = listView.NewListViewItem("ChatMemberListItem2");

            var itemComponent = newItem.GetComponent<ChannelMemberEntryView>();
            itemComponent.Setup(model);
            
            itemComponent.OnContextMenuRequested -= HandleItemContextMenuRequest;
            itemComponent.OnContextMenuRequested += HandleItemContextMenuRequest;

            return newItem;
        }

        private void HandleItemContextMenuRequest(string userId)
        {
            OnMemberContextMenuRequested?.Invoke(userId);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        public void SetMemberCount(int memberCount)
        {
            
        }
    }
}