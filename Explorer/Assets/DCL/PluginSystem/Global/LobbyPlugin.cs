using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterPreview;
using DCL.DebugUtilities;
using DCL.Input;
using DCL.Lobby;
using DCL.Profiles;
using DCL.Profiles.Self;
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
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly Arch.Core.World world;

        public LobbyPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IInputBlock inputBlock,
            IReadOnlyLoadingStatus loadingStatus,
            IDebugContainerBuilder debugContainerBuilder,
            ISelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            Arch.Core.World world)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
            this.debugContainerBuilder = debugContainerBuilder;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.world = world;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(LobbyPluginSettings settings, CancellationToken ct)
        {
            LobbyView prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.LobbyPrefab, ct: ct)).Value;

            mvcManager.RegisterController(new LobbyController(LobbyController.CreateLazily(prefab, null), inputBlock, loadingStatus, mvcManager,
                selfProfile, profileChangesBus, characterPreviewFactory, characterPreviewEventBus, settings.AvatarSettings, world));

            debugContainerBuilder
               .TryAddWidget("Lobby")?
               .AddSingleButton("Open", () => mvcManager.ShowAndForget(LobbyController.IssueCommand(new LobbyParameter(isStartup: false))));
        }
    }
}
