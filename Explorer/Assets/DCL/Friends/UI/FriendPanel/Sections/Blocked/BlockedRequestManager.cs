using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedRequestManager : FriendPanelRequestManager<BlockedUserView>
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly List<FriendProfile> blockedProfiles = new ();

        private FriendProfile? userProfile;

        public event Action<FriendProfile>? UnblockClicked;
        public event Action<FriendProfile, Vector2, BlockedUserView>? ContextMenuClicked;

        public BlockedRequestManager(
            IFriendsService friendsService,
            IFriendsEventBus friendsEventBus,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            int pageSize,
            int elementsMissingThreshold) : base(pageSize, elementsMissingThreshold, webRequestController, profileThumbnailCache)
        {
            this.friendsService = friendsService;
            this.friendsEventBus = friendsEventBus;
        }

        public override int GetCollectionCount() =>
            blockedProfiles.Count;

        protected override FriendProfile GetCollectionElement(int index) =>
            blockedProfiles[index];

        protected override void CustomiseElement(BlockedUserView elementView, int index)
        {
            elementView.UnblockButton.onClick.RemoveAllListeners();
            elementView.UnblockButton.onClick.AddListener(() => UnblockClicked?.Invoke(elementView.UserProfile));

            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));
        }

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            return MockedData();
        }

        private int MockedData()
        {
            blockedProfiles.Add(new FriendProfile(new Web3Address("0xbdfdd873d70fbf9273180f98ee30404115a1a674"), "NftIsland", true, URLAddress.FromString("http://profile-images.decentraland.org/entities/bafkreigb23roa4vsdhzrvqxw7xllbviota45yu3j3htduudbcjx2wiztny/face.png"), ProfileNameColorHelper.GetNameColor("NftIsland")));
            return blockedProfiles.Count;
        }

        protected override void ResetCollection() =>
            blockedProfiles.Clear();
    }
}
