using System.Collections.Generic;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatViewModels;
using SuperScrollView;
using UnityEngine;

namespace DCL.Chat._Refactor.ChatMessages.ScrollLists
{
    public class SuperScrollViewListController : MonoBehaviour, IChatListViewController
    {
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private ChatEntryView chatEntryPrefab;
        [SerializeField] private ChatEntryView chatEntryOwnPrefab;

        private readonly List<ChatMessageViewModel> viewModels = new ();

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
        }

        public void SetItems(IReadOnlyList<ChatMessageViewModel> items)
        {
            viewModels.Clear();
            viewModels.AddRange(items);
            loopListView.SetListItemCount(viewModels.Count);
            loopListView.RefreshAllShownItem();
        }

        public void AddItem(ChatMessageViewModel item)
        {
            viewModels.Add(item);
            loopListView.SetListItemCount(viewModels.Count);
            loopListView.RefreshAllShownItem();
        }

        public void UpdateItem(ChatMessageViewModel item)
        {
            // int index = viewModels.FindIndex(vm => vm.MessageId == item.MessageId);
            // if (index == -1) return;
            //
            // viewModels[index] = item;
            // loopListView.RefreshItemByItemIndex(index);
        }

        public void Clear()
        {
            viewModels.Clear();
            loopListView.SetListItemCount(0, false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= viewModels.Count)
                return null;

            var viewModel = viewModels[index];
            var prefab = viewModel.IsSentByOwnUser ? chatEntryOwnPrefab : chatEntryPrefab;

            var item = listView.NewListViewItem(prefab.name);
            var entryView = item.GetComponent<ChatEntryView>(); // SuperScrollView automatically instantiates the prefab

            // entryView.SetItemData(viewModel);
            // Wire up events from entryView to a local handler that forwards them if needed
            // e.g., entryView.OnProfileClicked += (userId, pos) => OnProfileContextMenuRequested?.Invoke(userId, pos);

            return item;
        }
    }
}