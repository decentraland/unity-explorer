using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.Sections.Blocked
{
    public class BlockedRequestManager : IDisposable
    {
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private Profile? userProfile;
        private List<Profile> blockedProfiles = new ();

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<Profile>? ElementClicked;
        public event Action<Profile>? UnblockClicked;
        public event Action<Profile>? ContextMenuClicked;

        public BlockedRequestManager(IProfileRepository profileRepository,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.profileRepository = profileRepository;
            this.profileCache = profileCache;
            this.web3IdentityCache = web3IdentityCache;
        }

        public void Dispose()
        {

        }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            BlockedUserView view = listItem.GetComponent<BlockedUserView>();
            view.Configure(blockedProfiles[index]);

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            view.UnblockButton.onClick.RemoveAllListeners();
            view.UnblockButton.onClick.AddListener(() => UnblockClicked?.Invoke(view.UserProfile));

            view.ContextMenuButton.onClick.RemoveAllListeners();
            view.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(view.UserProfile));

            return listItem;
        }

        public async UniTask Init(CancellationToken ct)
        {
            userProfile = await GetProfile(web3IdentityCache.Identity?.Address, ct);

            if (userProfile == null)
                throw new Exception($"Couldn't fetch user own profile for address {web3IdentityCache.Identity?.Address}");

            foreach (string blockedUserId in userProfile!.Blocked)
            {
                Profile? blockedProfile = await GetProfile(blockedUserId, ct);
                if (blockedProfile != null)
                    blockedProfiles.Add(blockedProfile);
            }

            HasElements = userProfile!.Blocked.Count > 0;
            WasInitialised = true;
        }

        public int GetElementsNumber() => blockedProfiles.Count;

        public void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            blockedProfiles.Clear();
        }

        private async UniTask<Profile?> GetProfile(string userId, CancellationToken ct)
        {
            Profile? profile = profileCache.Get(userId);

            if (profile == null)
            {
                profile = await profileRepository.GetAsync(userId, ct);
                if (profile != null)
                    profileCache.Set(userId, profile);
            }

            return profile;
        }
    }
}
