using SuperScrollView;
using System;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Paged rail backed by a <see cref="LoopListView2" />: only the cards in and around the viewport exist, so the list can be as
    ///     long as the friend list. Item snapping stays off in the prefab; the base paging snap moves the content instead.
    /// </summary>
    public class LobbyFriendsRailView : LobbyPagedRailView
    {
        [SerializeField] private LoopListView2 loopList = null!;

        public int ShownCount => loopList.ShownItemCount;

        public void Init(Func<LoopListView2, int, LoopListViewItem2> onGetItem)
        {
            ItemPrefabConfData prefabData = loopList.ItemPrefabDataList[0];
            LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();

            // The content is sized from this before any card has been laid out; the page snap relies on that width being right from the start
            initParam.mItemDefaultWithPaddingSize = ((RectTransform)prefabData.mItemPrefab.transform).rect.width + prefabData.mPadding;
            loopList.InitListView(0, onGetItem, initParam);
        }

        /// <summary>
        ///     Takes a pooled item for the next card. <paramref name="created" /> is true the first time this pooled item is handed out,
        ///     which is when its one-time callbacks must be wired.
        /// </summary>
        public LoopListViewItem2 NewItem(out LobbyFriendCardView card, out bool created)
        {
            LoopListViewItem2 item = loopList.NewListViewItem(loopList.ItemPrefabDataList[0].mItemPrefab.name);
            created = !item.IsInitHandlerCalled;

            if (created)
            {
                item.UserObjectData = item.GetComponent<LobbyFriendCardView>();
                item.IsInitHandlerCalled = true;
            }

            card = (LobbyFriendCardView)item.UserObjectData;
            return item;
        }

        public void SetCount(int count, bool rewind)
        {
            loopList.SetListItemCount(count, rewind);
            OnCountChanged(count, rewind);
        }

        public void RefreshShown() =>
            loopList.RefreshAllShownItem();

        public LobbyFriendCardView ShownCardAt(int shownIndex) =>
            (LobbyFriendCardView)loopList.GetShownItemByIndex(shownIndex).UserObjectData;
    }
}
