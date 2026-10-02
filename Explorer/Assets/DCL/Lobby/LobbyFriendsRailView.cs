using SuperScrollView;
using System;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Rail backed by a <see cref="LoopListView2" /> so only the cards around the viewport exist; item snapping stays off, the base paging snap moves the content.
    /// </summary>
    public class LobbyFriendsRailView : LobbyPagedRailView
    {
        [SerializeField] private LoopListView2 loopList = null!;

        private float cardStride;

        public int ShownCount => loopList.ShownItemCount;

        public void Init(Func<LoopListView2, int, LoopListViewItem2> onGetItem)
        {
            ItemPrefabConfData prefabData = loopList.ItemPrefabDataList[0];
            LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();

            // The content is sized from this before any card has been laid out; the page snap relies on that width being right from the start
            cardStride = ((RectTransform)prefabData.mItemPrefab.transform).rect.width + prefabData.mPadding;
            initParam.mItemDefaultWithPaddingSize = cardStride;
            loopList.InitListView(0, onGetItem, initParam);
        }

        /// <summary>
        ///     <paramref name="created" /> is true the first time this pooled item is handed out, when its one-time callbacks must be wired.
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

        protected override float CardStride() =>
            cardStride;
    }
}
