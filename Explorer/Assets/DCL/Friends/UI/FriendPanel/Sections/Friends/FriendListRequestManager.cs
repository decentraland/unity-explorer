using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendListRequestManager : FriendPanelRequestManager<FriendListUserView>
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly IProfileRepository profileRepository;
        private readonly LoopListView2 loopListView;
        private readonly List<FriendProfile> friends = new ();
        private readonly CancellationTokenSource addFriendProfileCts = new ();

        public event Action<FriendProfile, Vector2, FriendListUserView>? ContextMenuClicked;

        public FriendListRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            LoopListView2 loopListView,
            int pageSize,
            int elementsMissingThreshold) : base(pageSize, elementsMissingThreshold, webRequestController, profileThumbnailCache)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.profileRepository = profileRepository;
            this.loopListView = loopListView;

            this.friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += FriendRequestAccepted;
            this.friendEventBus.OnOtherUserAcceptedYourRequest += FriendRequestAccepted;
        }

        public override void Dispose()
        {
            base.Dispose();

            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= FriendRequestAccepted;
            friendEventBus.OnOtherUserAcceptedYourRequest -= FriendRequestAccepted;
            addFriendProfileCts.SafeCancelAndDispose();
        }

        private void FriendRequestAccepted(string friendId)
        {
            async UniTaskVoid AddNewFriendProfileAsync(CancellationToken ct)
            {
                Profile? newFriendProfile = await profileRepository.GetAsync(friendId, ct);
                if (newFriendProfile != null)
                {
                    friends.Add(newFriendProfile.ToFriendProfile());
                    friends.Sort((f1, f2) => string.Compare(f1.Name, f2.Name, StringComparison.Ordinal));
                    loopListView.RefreshAllShownItem();
                }
                else
                    ReportHub.LogError(new ReportData(ReportCategory.FRIENDS), $"Couldn't fetch new friend profile for user {friendId}");
            }

            AddNewFriendProfileAsync(addFriendProfileCts.Token).Forget();
        }

        public override int GetCollectionCount() =>
            friends.Count;

        protected override FriendProfile GetCollectionElement(int index) =>
            friends[index];

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            using PaginatedFriendsResult result = await friendsService.GetFriendsAsync(pageNumber, pageSize, ct);

            foreach (FriendProfile friend in result.Friends)
            {
                if (friends.Contains(friend)) continue;
                friends.Add(friend);
            }

            return result.TotalAmount;
            return FetchMockData();
        }

        protected override void ResetCollection() =>
            friends.Clear();

        private int FetchMockData()
        {
            friends.Add(new FriendProfile(new Web3Address("0x05dE05303EAb867D51854E8b4fE03F7acb0624d9"), "Test1", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x3a4401589ce5e65e0603df86b03c18c9fa8a05d1"), "Test2", true, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x381fb40e076f54687febb6235c65e91b12c47efd"), "Test3", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x76ce124714816aaf1d3548e5ee8b499bc4b31455"), "Test4", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0xcd110cd5dfc7f270fe137529ac17db8b81e28dd4"), "Test5", true, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x3faacc4e4287b82ccc1ca40adab0fc49a380b7ab"), "Test6", true, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x03d05ecbf55bcd0ee46b98e6a81d4baf91059a8b"), "Test7", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x4f7fe261619141ffa63fefee35bba886581292f4"), "Test8", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0xd545b9e0a5f3638a5026d1914cc9b47ed16b5ae9"), "Test9", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0xba7352cff5681b719daf33fa05e93153af8146c8"), "Test10", true, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x23e3d123f69fdd7f08a7c5685506bb344a12f1c4"), "Test11", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x5d327dcd9b4dae70ebf9c4ebb0576a1de97da520"), "Test12", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x97574fcd296f73fe34823973390ebe4b9b065300"), "Test13", false, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0x5d327dcd9b4dae70ebf9c4ebb0576a1de97da520"), "Test14", true, URLAddress.EMPTY));
            friends.Add(new FriendProfile(new Web3Address("0xb1d3f75bc57e744f7f6f8b014f1a0dc385649628"), "Test15", false, URLAddress.EMPTY));

            return friends.Count;
        }

        protected override void CustomiseElement(FriendListUserView elementView, int index)
        {
            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));

            elementView.ToggleOnlineStatus(false);
        }
    }
}
