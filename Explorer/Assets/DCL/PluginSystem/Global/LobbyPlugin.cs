using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Input;
using DCL.Lobby;
using DCL.RealmNavigation;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public partial class LobbyPlugin : IDCLGlobalPlugin<LobbyPlugin.LobbyPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public LobbyPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IInputBlock inputBlock,
            IReadOnlyLoadingStatus loadingStatus,
            IDebugContainerBuilder debugContainerBuilder)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(LobbyPluginSettings settings, CancellationToken ct)
        {
            LobbyView prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.LobbyPrefab, ct: ct)).Value;

            mvcManager.RegisterController(new LobbyController(LobbyController.CreateLazily(prefab, null), inputBlock, loadingStatus));

            debugContainerBuilder
               .TryAddWidget("Lobby")?
               .AddSingleButton("Open", () => mvcManager.ShowAndForget(LobbyController.IssueCommand(new LobbyParameter(isStartup: false))));
        }
    }
}
