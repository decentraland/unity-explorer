using MVC;
using SuperScrollView;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMemberListView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public struct MemberData
        {
            public string Id;
            public string Name;
            public string WalletId;
            public Sprite ProfilePicture;
            public ChatMemberConnectionStatus ConnectionStatus;
            public Color ProfileColor;
        }

        public delegate void VisibilityChangedDelegate(bool isVisible);

        /// <summary>
        ///
        /// </summary>
        public event VisibilityChangedDelegate VisibilityChanged;

        [SerializeField]
        private LoopListView2 loopListView;

        private List<MemberData> members = new();

        private ViewDependencies viewDependencies;
        private bool isInitialized;

        /// <summary>
        ///
        /// </summary>
        public bool IsVisible
        {
            get
            {
                return gameObject.activeInHierarchy;
            }

            set
            {
                if (value != IsVisible)
                {
                    gameObject.SetActive(value);
                    VisibilityChanged?.Invoke(value);
                }

            }
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            this.viewDependencies = dependencies;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="memberData"></param>
        public void SetData(List<MemberData> memberData)
        {
            members = memberData;

            if (!isInitialized)
            {
                loopListView.InitListView(members.Count, OnGetItemByIndex);
                isInitialized = true;
            }

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
            memberItem.ProfilePicture = members[index].ProfilePicture;
            memberItem.ConnectionStatus = members[index].ConnectionStatus;
            memberItem.Tag = members[index].WalletId;
            memberItem.ProfileColor = members[index].ProfileColor;
            memberItem.ContextMenuButtonClicked -= OnContextMenuButtonClicked;
            memberItem.ContextMenuButtonClicked += OnContextMenuButtonClicked;

            return newItem;
        }

        private void OnContextMenuButtonClicked(ChatMemberListViewItem listItem, Transform buttonPosition)
        {
            viewDependencies.GlobalUIViews.ShowUserProfileContextMenu(viewDependencies.ProfileCache.Get(listItem.Id), listItem.ProfileColor, buttonPosition);
        }
    }
}
