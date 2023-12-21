using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AuthenticationScreenFlow;
using DCL.DebugUtilities;
using DCL.ExplorePanel;
using DCL.Web3Authentication;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class Web3AuthenticationPlugin : IDCLGlobalPlugin<Web3AuthPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly MVCManager mvcManager;

        private CancellationTokenSource? cancellationTokenSource;

        public Web3AuthenticationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWeb3Authenticator web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder,
            MVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Authenticator = web3Authenticator;
            this.debugContainerBuilder = debugContainerBuilder;
            this.mvcManager = mvcManager;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Web3AuthPluginSettings settings, CancellationToken ct)
        {
            AuthenticationScreenView authScreenPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AuthScreenPrefab, ct: ct))
                                                       .Value.GetComponent<AuthenticationScreenView>();

            ControllerBase<AuthenticationScreenView, ControllerNoData>.ViewFactoryMethod? authScreenFactory = AuthenticationScreenController.CreateLazily(authScreenPrefab, null);
            mvcManager.RegisterController(new AuthenticationScreenController(authScreenFactory, web3Authenticator));

            mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand())
                      .ContinueWith(() =>
                       {
                           // TODO: connect next flow screen
                       })
                      .Forget();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            debugContainerBuilder.AddWidget("Web3 Authentication")
                                 .AddSingleButton("Login", Login);
        }

        private void Login()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LoginAsync(cancellationTokenSource.Token).Forget();
        }
    }

    public struct Web3AuthPluginSettings : IDCLPluginSettings
    {
        [field: Header(nameof(Web3AuthenticationPlugin) + "." + nameof(Web3AuthPluginSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject AuthScreenPrefab { get; private set; }
    }
}
