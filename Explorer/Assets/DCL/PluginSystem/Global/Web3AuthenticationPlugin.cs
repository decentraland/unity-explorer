using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AuthenticationScreenFlow;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Profiles.Self;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class Web3AuthenticationPlugin : IDCLGlobalPlugin<Web3AuthPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly MVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly Animator splashScreenAnimator;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly FeatureFlagsCache featureFlagsCache;

        private CancellationTokenSource? cancellationTokenSource;
        private AuthenticationScreenController authenticationScreenController = null!;

        public Web3AuthenticationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder,
            MVCManager mvcManager,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IRealmData realmData,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            Animator splashScreenAnimator,
            CharacterPreviewEventBus characterPreviewEventBus,
            FeatureFlagsCache featureFlagsCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Authenticator = web3Authenticator;
            this.debugContainerBuilder = debugContainerBuilder;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.realmData = realmData;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreenAnimator = splashScreenAnimator;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.featureFlagsCache = featureFlagsCache;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Web3AuthPluginSettings settings, CancellationToken ct)
        {
            AuthenticationScreenView authScreenPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AuthScreenPrefab, ct: ct)).Value;

            ControllerBase<AuthenticationScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory = AuthenticationScreenController.CreateLazily(authScreenPrefab, null);

            authenticationScreenController = new AuthenticationScreenController(authScreenFactory, web3Authenticator, selfProfile, webBrowser, storedIdentityProvider, characterPreviewFactory, splashScreenAnimator, characterPreviewEventBus, featureFlagsCache);
            mvcManager.RegisterController(authenticationScreenController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoginFromDebugPanelSystem.InjectToWorld(ref builder, debugContainerBuilder, web3Authenticator, mvcManager, realmData);
            authenticationScreenController.SetWorld(builder.World);
        }
    }

    public struct Web3AuthPluginSettings : IDCLPluginSettings
    {
        [field: Header(nameof(Web3AuthenticationPlugin) + "." + nameof(Web3AuthPluginSettings))]
        [field: Space]
        [field: SerializeField]
        public AuthScreenObjectRef AuthScreenPrefab { get; private set; }

        [Serializable]
        public class AuthScreenObjectRef : ComponentReference<AuthenticationScreenView>
        {
            public AuthScreenObjectRef(string guid) : base(guid) { }
        }
    }
}
