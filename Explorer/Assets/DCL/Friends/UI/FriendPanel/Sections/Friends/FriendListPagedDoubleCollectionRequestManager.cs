using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendListPagedDoubleCollectionRequestManager : FriendPanelDoubleCollectionRequestManager<FriendListUserView>
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;

        private List<Profile> onlineFriends = new ();
        private List<Profile> offlineFriends = new ();

        public event Action<Profile>? JumpInClicked;
        public event Action<Profile>? ContextMenuClicked;

        public FriendListPagedDoubleCollectionRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            int pageSize) : base(friendsService, friendEventBus, pageSize, FriendPanelStatus.ONLINE, FriendPanelStatus.OFFLINE, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {

        }

        public override void Dispose()
        {

        }

        protected override Profile GetFirstCollectionElement(int index) =>
            onlineFriends[index];

        protected override Profile GetSecondCollectionElement(int index) =>
            offlineFriends[index];

        public override int GetFirstCollectionCount() =>
            onlineFriends.Count;

        public override int GetSecondCollectionCount() =>
            offlineFriends.Count;

        protected override void CustomiseElement(FriendListUserView elementView, int collectionIndex, FriendPanelStatus section)
        {
            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile));
            elementView.JumpInButton.onClick.RemoveAllListeners();
            elementView.JumpInButton.onClick.AddListener(() => JumpInClicked?.Invoke(elementView.UserProfile));
            if (section == FriendPanelStatus.OFFLINE)
                elementView.SetOnlineStatus(OnlineStatus.OFFLINE);
            //TODO (Lorenzo): set online status
            // elementView.OnlineStatus.SetText();
        }

        protected async override UniTask FetchInitialData(CancellationToken ct)
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

        }
    }
}
