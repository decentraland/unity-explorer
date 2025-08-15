using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
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
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly SplashScreen splashScreen;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly Arch.Core.World world;
        private readonly AudioMixerVolumesController audioMixerVolumesController;

        private CancellationTokenSource? cancellationTokenSource;
        private AuthenticationScreenController authenticationScreenController = null!;

        public Web3AuthenticationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IRealmData realmData,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            SplashScreen splashScreen,
            AudioMixerVolumesController audioMixerVolumesController,
            CharacterPreviewEventBus characterPreviewEventBus,
            Arch.Core.World world
        )
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
            this.splashScreen = splashScreen;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.world = world;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Web3AuthPluginSettings settings, CancellationToken ct)
        {
            AuthenticationScreenView authScreenPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AuthScreenPrefab, ct: ct)).Value;
            ControllerBase<AuthenticationScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory = AuthenticationScreenController.CreateLazily(authScreenPrefab, null);

            authenticationScreenController = new AuthenticationScreenController(authScreenFactory, web3Authenticator, selfProfile, webBrowser, storedIdentityProvider, characterPreviewFactory, splashScreen, characterPreviewEventBus, audioMixerVolumesController, settings.BuildData, world, settings.EmotesSettings);
            mvcManager.RegisterController(authenticationScreenController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoginFromDebugPanelSystem.InjectToWorld(ref builder, debugContainerBuilder, web3Authenticator, mvcManager, realmData);
        }
    }

    public struct Web3AuthPluginSettings : IDCLPluginSettings
    {
        [field: Header(nameof(Web3AuthenticationPlugin) + "." + nameof(Web3AuthPluginSettings))]
        [field: Space]
        [field: SerializeField] public AuthScreenObjectRef AuthScreenPrefab { get; private set; }
        [field: SerializeField] public BuildData BuildData { get; private set; }

        [field: Space]
        [field: SerializeField] public AuthScreenEmotesSettings EmotesSettings { get; private set; }

        [Serializable]
        public class AuthScreenObjectRef : ComponentReference<AuthenticationScreenView>
        {
            public AuthScreenObjectRef(string guid) : base(guid) { }
        }
    }
}
