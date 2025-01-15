using Cysharp.Threading.Tasks;
using DCL.Profiles;
using SuperScrollView;
using System;
using System.Collections.Generic;
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
        private bool excludeOnline = false;
        private bool excludeOffline = false;

        public bool HasFriends { get; private set; }
        public bool WasInitialised { get; private set; }
        public event Action? OnlineFolderClicked;
        public event Action? OfflineFolderClicked;
        public event Action<Profile>? FriendClicked;

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
            int onlineFriendMarker = excludeOnline ? 0 : onlineFriends.Count;
            if (onlineFriends.Count == 0) onlineFriendMarker++; //Count the empty element
            int offlineFriendMarker = excludeOffline ? 0 : offlineFriends.Count;
            if (offlineFriends.Count == 0) offlineFriendMarker++; //Count the empty element

            if (index == 0)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[STATUS_ELEMENT_INDEX].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(FriendPanelStatus.ONLINE, onlineFriends.Count);
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
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
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => FriendClicked?.Invoke(profile);
                }
            }
            else if (index == onlineFriendMarker + 1)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[STATUS_ELEMENT_INDEX].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(FriendPanelStatus.OFFLINE, offlineFriends.Count);
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
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
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => FriendClicked?.Invoke(profile);
                }
            }

            return listItem;
        }

        private void FolderClick(bool isFolded, FriendPanelStatus panelStatus)
        {
            if (panelStatus == FriendPanelStatus.ONLINE)
            {
                excludeOnline = isFolded;
                OnlineFolderClicked?.Invoke();
            }
            else if (panelStatus == FriendPanelStatus.OFFLINE)
            {
                excludeOffline = isFolded;
                OfflineFolderClicked?.Invoke();
            }
        }

        public int GetElementsNumber()
        {
            int count = 2;

            if (!excludeOnline)
                count += onlineFriends.Count;

            if (!excludeOffline)
                count += offlineFriends.Count;

            if (onlineFriends.Count == 0)
                count++;

            if (offlineFriends.Count == 0)
                count++;

            return count;
        }

        public async UniTask Init(CancellationToken ct)
        {
            // PaginatedFriendsResult result = await friendsService.GetFriendsAsync(pageNumber, pageSize, ct);
            offlineFriends.Add(Profile.NewRandomProfile("0x05dE05303EAb867D51854E8b4fE03F7acb0624d9"));
            offlineFriends.Add(Profile.NewRandomProfile("0x3a4401589ce5e65e0603df86b03c18c9fa8a05d1"));
            offlineFriends.Add(Profile.NewRandomProfile("0x381fb40e076f54687febb6235c65e91b12c47efd"));
            onlineFriends.Add(Profile.NewRandomProfile("0x76ce124714816aaf1d3548e5ee8b499bc4b31455"));
            onlineFriends.Add(Profile.NewRandomProfile("0xcd110cd5dfc7f270fe137529ac17db8b81e28dd4"));
            onlineFriends.Add(Profile.NewRandomProfile("0x3faacc4e4287b82ccc1ca40adab0fc49a380b7ab"));
            onlineFriends.Add(Profile.NewRandomProfile("0x03d05ecbf55bcd0ee46b98e6a81d4baf91059a8b"));
            onlineFriends.Add(Profile.NewRandomProfile("0x4f7fe261619141ffa63fefee35bba886581292f4"));
            onlineFriends.Add(Profile.NewRandomProfile("0xd545b9e0a5f3638a5026d1914cc9b47ed16b5ae9"));
            onlineFriends.Add(Profile.NewRandomProfile("0xba7352cff5681b719daf33fa05e93153af8146c8"));
            onlineFriends.Add(Profile.NewRandomProfile("0x23e3d123f69fdd7f08a7c5685506bb344a12f1c4"));
            onlineFriends.Add(Profile.NewRandomProfile("0x5d327dcd9b4dae70ebf9c4ebb0576a1de97da520"));
            onlineFriends.Add(Profile.NewRandomProfile("0x97574fcd296f73fe34823973390ebe4b9b065300"));
            onlineFriends.Add(Profile.NewRandomProfile("0x5d327dcd9b4dae70ebf9c4ebb0576a1de97da520"));
            onlineFriends.Add(Profile.NewRandomProfile("0xb1d3f75bc57e744f7f6f8b014f1a0dc385649628"));
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
