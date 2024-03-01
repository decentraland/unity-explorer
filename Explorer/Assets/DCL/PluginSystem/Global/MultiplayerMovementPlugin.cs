using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.ECS.System;
using DCL.Multiplayer.Movement.Settings;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IArchipelagoIslandRoom room;
        private readonly ICharacterObject characterObject;

        private ProvidedAsset<MultiplayerSpatialStateSettings> settings;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, IArchipelagoIslandRoom room, ICharacterObject characterObject)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.room = room;
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
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, room, settings.Value, characterObject.Controller);
            ReplicasMovementNetReceiveSystem.InjectToWorld(ref builder, room, settings.Value);
        }
    }
}
