using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.UI.Requests;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IWeb3IdentityCache identityCache;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;
        private readonly ISelfProfile selfProfile;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private RPCFriendsService? friendsService;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource,
            IWeb3IdentityCache identityCache,
            FeatureFlagsCache featureFlagsCache,
            ISelfProfile selfProfile,
            IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            IInputBlock inputBlock)
        {
            this.dclUrlSource = dclUrlSource;
            this.identityCache = identityCache;
            this.featureFlagsCache = featureFlagsCache;
            this.selfProfile = selfProfile;
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            this.inputBlock = inputBlock;
        }

        public void Dispose()
        {
            lifeCycleCancellationToken.SafeCancelAndDispose();
            friendsService?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            var friendsCache = new FriendsCache();

            friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus, identityCache, friendsCache, selfProfile);

            if (featureFlagsCache.Configuration.IsEnabled("alpha-friends-enabled"))
            {
                // Fire and forget as this task will never finish
                var cts = CancellationTokenSource.CreateLinkedTokenSource(lifeCycleCancellationToken.Token, ct);

                friendsService.SubscribeToIncomingFriendshipEventsAsync(cts.Token).Forget();
                friendsService.SubscribeToConnectivityStatus(cts.Token).Forget();
            }

            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                identityCache, friendsService, profileRepository, webRequestController,
                inputBlock);

            mvcManager.RegisterController(friendRequestController);
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public FriendRequestAssetReference FriendRequestPrefab { get; set; }

        [Serializable]
        public class FriendRequestAssetReference : ComponentReference<FriendRequestView>
        {
            public FriendRequestAssetReference(string guid) : base(guid) { }
        }
    }
}
