using Cysharp.Threading.Tasks;
using DCL.Profiles;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendListPagedRequestManager : IDisposable
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;

        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly int pageSize;

        private int pageNumber = 0;
        private int totalFetched = 0;
        private List<Profile> onlineFriends = new ();
        private List<Profile> offlineFriends = new ();

        public bool HasFriends { get; private set; }
        public bool WasInitialised { get; private set; }

        public FriendListPagedRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            int pageSize)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.pageSize = pageSize;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = null;
            int onlineFriendMarker = onlineFriends.Count;
            if (onlineFriends.Count == 0) onlineFriendMarker++;
            int offlineFriendMarker = offlineFriends.Count;
            if (offlineFriends.Count == 0) offlineFriendMarker++;

            if (index == 0)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[STATUS_ELEMENT_INDEX].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(FriendPanelStatus.ONLINE, onlineFriends.Count);
            }
            else if (index > 0 && index <= onlineFriendMarker)
            {
                if (onlineFriends.Count == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[EMPTY_ELEMENT_INDEX].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[USER_ELEMENT_INDEX].mItemPrefab.name);
                    FriendListUserView friendListUserView = listItem.GetComponent<FriendListUserView>();
                    friendListUserView.Configure(onlineFriends[index - 1]);
                }
            }
            else if (index == onlineFriendMarker + 1)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[STATUS_ELEMENT_INDEX].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(FriendPanelStatus.OFFLINE, offlineFriends.Count);
            }
            else if (index > onlineFriendMarker + 1 && index <= onlineFriendMarker + 1 + offlineFriendMarker + 1)
            {
                if (offlineFriends.Count == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[EMPTY_ELEMENT_INDEX].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[USER_ELEMENT_INDEX].mItemPrefab.name);
                    FriendListUserView friendListUserView = listItem.GetComponent<FriendListUserView>();
                    friendListUserView.Configure(offlineFriends[index - onlineFriendMarker - 2]);
                }
            }

            return listItem;
        }

        public int GetElementsNumber()
        {
            int count = 2 + onlineFriends.Count + offlineFriends.Count;

            if (onlineFriends.Count == 0)
                count++;

            if (offlineFriends.Count == 0)
                count++;

            return count;
        }

        public async UniTask Init(CancellationToken ct)
        {
            PaginatedFriendsResult result = await friendsService.GetFriendsAsync(pageNumber, pageSize, ct);
            offlineFriends.Add(Profile.NewRandomProfile("0x05dE05303EAb867D51854E8b4fE03F7acb0624d9"));
            offlineFriends.Add(Profile.NewRandomProfile("0x05dE05303EAb867D51854E8b4fE03F7acb0624d9"));
            onlineFriends.Add(Profile.NewRandomProfile("0x05dE05303EAb867D51854E8b4fE03F7acb0624d9"));
            HasFriends = onlineFriends.Count + offlineFriends.Count > 0;
            WasInitialised = true;
        }

        public void Reset()
        {
            HasFriends = false;
            WasInitialised = false;
            pageNumber = 0;
            totalFetched = 0;
        }
    }
}
