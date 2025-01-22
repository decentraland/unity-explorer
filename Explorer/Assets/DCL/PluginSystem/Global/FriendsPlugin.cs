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
using DCL.Web3;
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
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private RPCFriendsService? friendsService;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource,
            IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            FeatureFlagsCache featureFlagsCache,
            IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            IInputBlock inputBlock)
        {
            this.dclUrlSource = dclUrlSource;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.featureFlagsCache = featureFlagsCache;
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
                friendEventBus, profileRepository, identityCache, friendsCache);

            if (featureFlagsCache.Configuration.IsEnabled("alpha-friends-enabled"))
            {
                // Fire and forget as this task will never finish
                friendsService.SubscribeToIncomingFriendshipEventsAsync(
                                   CancellationTokenSource.CreateLinkedTokenSource(lifeCycleCancellationToken.Token, ct).Token)
                              .Forget();
            }

            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                identityCache, friendsService, profileRepository, webRequestController,
                inputBlock);

            mvcManager.RegisterController(friendRequestController);

            Bleh().Forget();

            async UniTaskVoid Bleh()
            {
                await UniTask.Delay(30000, cancellationToken: ct);

                await mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                {
                    DestinationUser = new Web3Address("0xc9c29ab98e6bc42015985165a11153f564e9f8c2"),
                    // Request = new FriendRequest(Guid.NewGuid().ToString(), DateTime.UtcNow,
                    //     identityCache.EnsuredIdentity().Address,
                    //     "0xc9c29ab98e6bc42015985165a11153f564e9f8c2",
                    //     "aowidjaiodjioawjdioajdoadwjio"),
                }), ct);
            }
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
