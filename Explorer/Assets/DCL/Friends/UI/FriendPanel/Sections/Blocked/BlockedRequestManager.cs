using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedRequestManager : FriendPanelRequestManager<BlockedUserView>
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private FriendProfile? userProfile;
        private List<FriendProfile> blockedProfiles = new ();

        public event Action<FriendProfile>? UnblockClicked;
        public event Action<FriendProfile>? ContextMenuClicked;

        public BlockedRequestManager(IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            int pageSize,
            int elementsMissingThreshold) : base(pageSize, elementsMissingThreshold, webRequestController, profileThumbnailCache)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
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
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile));
        }

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            //TODO: Implement this with the new social service logic
            return 0;
        }

        protected override void ResetCollection() =>
            blockedProfiles.Clear();
    }
}
