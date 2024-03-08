using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.ECS.System;
using DCL.Multiplayer.Movement.Settings;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRoomHub roomHub;
        private readonly ICharacterObject characterObject;

        private ProvidedAsset<MultiplayerMovementSettings> settings;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, IRoomHub roomHub, ICharacterObject characterObject)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.roomHub = roomHub;
            this.characterObject = characterObject;
        }

        public void Dispose()
        {
            settings.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.spatialStateSettings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, roomHub, settings.Value, characterObject.Controller);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, roomHub, settings.Value);
        }
    }
}
