using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AuthenticationScreenFlow;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.DebugUtilities;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class Web3AuthenticationPlugin : IDCLGlobalPlugin<Web3AuthPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly MVCManager mvcManager;
        private readonly IProfileRepository profileRepository;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private CancellationTokenSource? cancellationTokenSource;
        private AuthenticationScreenController authenticationScreenController;

        public Web3AuthenticationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder,
            MVCManager mvcManager,
            IProfileRepository profileRepository,
            IWebBrowser webBrowser,
            IRealmData realmData,
            IWeb3IdentityCache storedIdentityProvider,
            IComponentPoolsRegistry poolsRegistry)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Authenticator = web3Authenticator;
            this.debugContainerBuilder = debugContainerBuilder;
            this.mvcManager = mvcManager;
            this.profileRepository = profileRepository;
            this.webBrowser = webBrowser;
            this.realmData = realmData;
            this.storedIdentityProvider = storedIdentityProvider;
            this.poolsRegistry = poolsRegistry;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Web3AuthPluginSettings settings, CancellationToken ct)
        {
            AuthenticationScreenView authScreenPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AuthScreenPrefab, ct: ct)).Value;

            ControllerBase<AuthenticationScreenView, ControllerNoData>.ViewFactoryMethod? authScreenFactory = AuthenticationScreenController.CreateLazily(authScreenPrefab, null);

            ICharacterPreviewFactory characterPreviewFactory = new CharacterPreviewFactory(poolsRegistry);

            authenticationScreenController = new AuthenticationScreenController(authScreenFactory, web3Authenticator, profileRepository, webBrowser, storedIdentityProvider, characterPreviewFactory);
            mvcManager.RegisterController(authenticationScreenController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoginFromDebugPanelSystem.InjectToWorld(ref builder, debugContainerBuilder, web3Authenticator, mvcManager, realmData);
            authenticationScreenController.SetWorld(builder.World, arguments.PlayerEntity);
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
