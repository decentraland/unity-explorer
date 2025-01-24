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

        private Profile? userProfile;
        private List<Profile> blockedProfiles = new ();

        public event Action<Profile>? UnblockClicked;
        public event Action<Profile>? ContextMenuClicked;

        public BlockedRequestManager(IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            int pageSize,
            int elementsMissingThreshold) : base(pageSize, elementsMissingThreshold, webRequestController)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
        }

        public override int GetCollectionCount() =>
            blockedProfiles.Count;

        protected override Profile GetCollectionElement(int index) =>
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
            userProfile = await profileRepository.GetAsync(web3IdentityCache.Identity?.Address, ct);

            if (userProfile == null)
                throw new Exception($"Couldn't fetch user own profile for address {web3IdentityCache.Identity?.Address}");

            foreach (string blockedUserId in userProfile!.Blocked)
            {
                Profile? blockedProfile = await profileRepository.GetAsync(blockedUserId, ct);
                if (blockedProfile != null)
                    blockedProfiles.Add(blockedProfile);
            }

            return userProfile!.Blocked.Count;
        }

        protected override void ResetCollection() =>
            blockedProfiles.Clear();
    }
}
