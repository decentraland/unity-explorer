using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedPanelList : FriendPanelRequestManager<BlockedUserView>
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly List<BlockedProfile> blockedProfiles = new ();

        public event Action<BlockedProfile>? UnblockClicked;
        public event Action<BlockedProfile, Vector2, BlockedUserView>? ContextMenuClicked;
        public event Action? NoUserInCollection;
        public event Action? AtLeastOneUserInCollection;

        public BlockedPanelList(
            IFriendsService friendsService,
            IFriendsEventBus friendsEventBus,
            ProfileRepositoryWrapper profileDataProvider,
            LoopListView2 loopListView,
            int pageSize,
            int elementsMissingThreshold) : base(profileDataProvider, loopListView, pageSize, elementsMissingThreshold)
        {
            this.friendsService = friendsService;
            this.friendsEventBus = friendsEventBus;

            friendsEventBus.OnYouBlockedProfile += BlockProfile;
            friendsEventBus.OnYouUnblockedProfile += UnblockProfile;
        }

        public override void Dispose()
        {
            base.Dispose();

            friendsEventBus.OnYouBlockedProfile -= BlockProfile;
            friendsEventBus.OnYouUnblockedProfile -= UnblockProfile;
        }

        private void BlockProfile(BlockedProfile profile)
        {
            int previousCount = blockedProfiles.Count;
            if (blockedProfiles.Contains(profile)) return;

            blockedProfiles.Add(profile);
            FriendsSorter.SortFriendList(blockedProfiles);

            RefreshLoopList();

            if (previousCount == 0)
                AtLeastOneUserInCollection?.Invoke();
        }

        private void UnblockProfile(BlockedProfile profile)
        {
            if (blockedProfiles.Remove(profile))
                RefreshLoopList();
            else
                return;

            if (blockedProfiles.Count == 0)
                NoUserInCollection?.Invoke();
        }

        public override int GetCollectionCount() =>
            blockedProfiles.Count;

        protected override FriendProfile GetCollectionElement(int index) =>
            blockedProfiles[index];

        protected override void CustomiseElement(BlockedUserView elementView, int index)
        {
            BlockedProfile element = blockedProfiles[index];

            elementView.UnblockButton.onClick.RemoveAllListeners();
            elementView.UnblockButton.onClick.AddListener(() => UnblockClicked?.Invoke(element));

            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(element, elementView.ContextMenuButton.transform.position, elementView));

            elementView.BlockedDate = element.Timestamp;
        }

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            using PaginatedBlockedProfileResult result = await friendsService.GetBlockedUsersAsync(pageNumber, pageSize, ct);

            foreach (var blockedProfile in result.BlockedProfiles)
                if (!blockedProfiles.Contains(blockedProfile))
                    blockedProfiles.Add(blockedProfile);

            FriendsSorter.SortFriendList(blockedProfiles);

            return result.TotalAmount;
        }

        protected override void ResetCollection() =>
            blockedProfiles.Clear();
    }
}
