using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendListPagedDoubleCollectionRequestManager : FriendPanelDoubleCollectionRequestManager<FriendListUserView>
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;

        private readonly LoopListView2 loopListView;
        private readonly RealmProfileRepository profileRepository;
        private readonly CancellationTokenSource addFriendProfileCts = new ();
        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly List<FriendProfile> onlineFriends = new ();
        private readonly List<FriendProfile> offlineFriends = new ();

        public event Action<FriendProfile>? JumpInClicked;
        public event Action<FriendProfile, Vector2, FriendListUserView>? ContextMenuClicked;
        public event Action? NoFriendsInCollections;
        public event Action? AtLeastOneFriendInCollections;

        public FriendListPagedDoubleCollectionRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            RealmProfileRepository profileRepository,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            LoopListView2 loopListView,
            int pageSize,
            int elementsMissingThreshold) : base(friendsService, friendEventBus, webRequestController, profileThumbnailCache, pageSize, elementsMissingThreshold, FriendPanelStatus.ONLINE, FriendPanelStatus.OFFLINE, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {
            this.profileRepository = profileRepository;
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.loopListView = loopListView;

            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += FriendRequestAccepted;
            friendEventBus.OnOtherUserAcceptedYourRequest += FriendRequestAccepted;
            friendEventBus.OnYouRemovedFriend += RemoveFriend;
            friendEventBus.OnOtherUserRemovedTheFriendship += RemoveFriend;

            friendsConnectivityStatusTracker.OnFriendBecameOnline += FriendBecameOnline;
            friendsConnectivityStatusTracker.OnFriendBecameAway += FriendBecameAway;
            friendsConnectivityStatusTracker.OnFriendBecameOffline += FriendBecameOffline;
        }

        public override void Dispose()
        {
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= FriendRequestAccepted;
            friendEventBus.OnOtherUserAcceptedYourRequest -= FriendRequestAccepted;
            friendEventBus.OnYouRemovedFriend -= RemoveFriend;
            friendEventBus.OnOtherUserRemovedTheFriendship -= RemoveFriend;
            addFriendProfileCts.SafeCancelAndDispose();

            friendsConnectivityStatusTracker.OnFriendBecameOnline -= FriendBecameOnline;
            friendsConnectivityStatusTracker.OnFriendBecameAway -= FriendBecameAway;
            friendsConnectivityStatusTracker.OnFriendBecameOffline -= FriendBecameOffline;
        }

        private void AddNewFriendProfile(FriendProfile friendProfile, OnlineStatus onlineStatus)
        {
            int previousTotalCount = offlineFriends.Count + onlineFriends.Count;

            if (onlineStatus == OnlineStatus.OFFLINE)
            {
                offlineFriends.Add(friendProfile);
                FriendsSorter.SortFriendList(offlineFriends);
            }
            else
            {
                onlineFriends.Add(friendProfile);
                FriendsSorter.SortFriendList(onlineFriends);
            }

            if (previousTotalCount == 0)
                AtLeastOneFriendInCollections?.Invoke();
        }

        private void FriendBecameOnline(FriendProfile friendProfile)
        {
            offlineFriends.Remove(friendProfile);
            if (!onlineFriends.Contains(friendProfile))
                AddNewFriendProfile(friendProfile, OnlineStatus.ONLINE);

            RefreshLoopList();
        }

        private void FriendBecameAway(FriendProfile friendProfile)
        {
            offlineFriends.Remove(friendProfile);
            if (!onlineFriends.Contains(friendProfile))
                AddNewFriendProfile(friendProfile, OnlineStatus.AWAY);

            RefreshLoopList();
        }

        private void FriendBecameOffline(FriendProfile friendProfile)
        {
            onlineFriends.Remove(friendProfile);
            if (!offlineFriends.Contains(friendProfile))
                AddNewFriendProfile(friendProfile, OnlineStatus.OFFLINE);

            RefreshLoopList();
        }

        private void RefreshLoopList()
        {
            loopListView.SetListItemCount(GetElementsNumber(), false);
            loopListView.RefreshAllShownItem();
        }

        private void FriendRequestAccepted(string friendId)
        {
            async UniTaskVoid AddNewFriendProfileAsync(CancellationToken ct)
            {
                // TODO: we should avoid requesting the profile.. instead the service should emit a FriendProfile
                Profile? newFriendProfile = await profileRepository.GetAsync(friendId,0, (URLDomain?)null, ct);

                if (newFriendProfile != null)
                {
                    FriendProfile friendProfile = newFriendProfile.ToFriendProfile();
                    if (!offlineFriends.Contains(friendProfile) && !onlineFriends.Contains(friendProfile))
                    {
                        AddNewFriendProfile(friendProfile, OnlineStatus.OFFLINE);
                        RefreshLoopList();
                    }
                }
                else
                    ReportHub.LogError(new ReportData(ReportCategory.FRIENDS), $"Couldn't fetch new friend profile for user {friendId}");
            }

            AddNewFriendProfileAsync(addFriendProfileCts.Token).Forget();
        }

        private void RemoveFriend(string userid)
        {
            onlineFriends.RemoveAll(friendProfile => friendProfile.Address.ToString().Equals(userid));
            offlineFriends.RemoveAll(friendProfile => friendProfile.Address.ToString().Equals(userid));
            RefreshLoopList();

            if (offlineFriends.Count + onlineFriends.Count == 0)
                NoFriendsInCollections?.Invoke();
        }

        protected override FriendProfile GetFirstCollectionElement(int index) =>
            onlineFriends[index];

        protected override FriendProfile GetSecondCollectionElement(int index) =>
            offlineFriends[index];

        protected override int GetFirstCollectionCount() =>
            onlineFriends.Count;

        protected override int GetSecondCollectionCount() =>
            offlineFriends.Count;

        protected override void CustomiseElement(FriendListUserView elementView, int collectionIndex, FriendPanelStatus section)
        {
            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));

            elementView.JumpInButton.onClick.RemoveAllListeners();
            elementView.JumpInButton.onClick.AddListener(() => JumpInClicked?.Invoke(elementView.UserProfile));

            elementView.ToggleOnlineStatus(true);

            elementView.SetOnlineStatus(friendsConnectivityStatusTracker.GetFriendStatus(elementView.UserProfile.Address));
        }

        protected override void ResetCollections()
        {
            onlineFriends.Clear();
            offlineFriends.Clear();
        }

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            using PaginatedFriendsResult result = await friendsService.GetFriendsAsync(pageNumber, pageSize, ct);

            foreach (FriendProfile friend in result.Friends)
            {
                if (offlineFriends.Contains(friend) || onlineFriends.Contains(friend)) continue;
                offlineFriends.Add(friend);
            }

            FriendsSorter.SortFriendList(offlineFriends);

            return result.TotalAmount;
        }
    }
}
