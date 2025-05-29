using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
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
        private readonly List<FriendProfile> friends = new ();
        private readonly CancellationTokenSource addFriendProfileCts = new ();

        public event Action<FriendProfile, Vector2, FriendListUserView>? ContextMenuClicked;
        public event Action<FriendProfile>? JumpInClicked;
        public event Action<FriendProfile>? ChatClicked;

        public FriendListRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IProfileRepository profileRepository,
            LoopListView2 loopListView,
            ProfileRepositoryWrapper profileDataProvider,
            int pageSize,
            int elementsMissingThreshold) :
            base(profileDataProvider, loopListView, pageSize, elementsMissingThreshold)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.profileRepository = profileRepository;

            this.friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += FriendRequestAccepted;
            this.friendEventBus.OnOtherUserAcceptedYourRequest += FriendRequestAccepted;
            this.friendEventBus.OnYouRemovedFriend += RemoveFriend;
            this.friendEventBus.OnYouBlockedByUser += RemoveFriend;
            this.friendEventBus.OnYouBlockedProfile += RemoveFriend;
            this.friendEventBus.OnOtherUserRemovedTheFriendship += RemoveFriend;
        }

        public override void Dispose()
        {
            base.Dispose();

            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= FriendRequestAccepted;
            friendEventBus.OnOtherUserAcceptedYourRequest -= FriendRequestAccepted;
            friendEventBus.OnYouRemovedFriend -= RemoveFriend;
            friendEventBus.OnOtherUserRemovedTheFriendship -= RemoveFriend;
            friendEventBus.OnYouBlockedByUser -= RemoveFriend;
            friendEventBus.OnYouBlockedProfile -= RemoveFriend;
            addFriendProfileCts.SafeCancelAndDispose();
        }

        private void AddNewFriendProfile(FriendProfile friendProfile)
        {
            friends.Add(friendProfile);
            FriendsSorter.SortFriendList(friends);
        }

        private void FriendRequestAccepted(string friendId)
        {
            AddNewFriendProfileAsync(addFriendProfileCts.Token).Forget();

            async UniTaskVoid AddNewFriendProfileAsync(CancellationToken ct)
            {
                // TODO: we should avoid requesting the profile.. instead the service should emit a FriendProfile
                Profile? newFriendProfile = await profileRepository.GetAsync(friendId, ct);

                if (newFriendProfile != null)
                {
                    AddNewFriendProfile(newFriendProfile.ToFriendProfile());
                    RefreshLoopList();
                }
                else
                    ReportHub.LogError(new ReportData(ReportCategory.FRIENDS), $"Couldn't fetch new friend profile for user {friendId}");
            }
        }

        private void RemoveFriend(BlockedProfile profile) =>
            RemoveFriend(profile.Address);

        private void RemoveFriend(string userid)
        {
            if (friends.RemoveAll(friendProfile => friendProfile.Address.ToString().Equals(userid)) > 0)
                RefreshLoopList();
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

            FriendsSorter.SortFriendList(friends);

            return result.TotalAmount;
        }

        protected override void ResetCollection() =>
            friends.Clear();

        protected override void CustomiseElement(FriendListUserView elementView, int index)
        {
            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));

            elementView.JumpInButton.onClick.RemoveAllListeners();
            elementView.JumpInButton.onClick.AddListener(() => JumpInClicked?.Invoke(elementView.UserProfile));

            elementView.ChatButton.onClick.RemoveAllListeners();
            elementView.ChatButton.onClick.AddListener(() => ChatClicked?.Invoke(elementView.UserProfile));

            elementView.ToggleOnlineStatus(false);
        }
    }
}
