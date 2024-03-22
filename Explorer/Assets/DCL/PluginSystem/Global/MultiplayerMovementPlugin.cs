using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using System.Threading;
using PlayerMovementNetSendSystem = DCL.Multiplayer.Movement.Systems.PlayerMovementNetSendSystem;
using RemotePlayersMovementSystem = DCL.Multiplayer.Movement.Systems.RemotePlayersMovementSystem;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MultiplayerMovementMessageBus messageBus;

        private ProvidedAsset<MultiplayerMovementSettings> settings;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, MultiplayerMovementMessageBus messageBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.messageBus = messageBus;
        }

        public void Dispose()
        {
            messageBus.Dispose();
            settings.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.spatialStateSettings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, messageBus, settings.Value);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, messageBus, settings.Value);
        }
    }
}
