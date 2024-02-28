using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.ECS.System;
using DCL.Multiplayer.Movement.Settings;
using LiveKit.Rooms.DataPipes;
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
            var world = builder.World;

            CharacterAnimationComponent playerAnimationComponent = world.Get<CharacterAnimationComponent>(arguments.PlayerEntity);
            StunComponent playerStunComponent = world.Get<StunComponent>(arguments.PlayerEntity);

            PlayerNetMovementSendSystem.InjectToWorld(ref builder, room, settings.Value, characterObject.Controller, playerAnimationComponent, playerStunComponent);
            PlayersReplicasNetMovementSystem.InjectToWorld(ref builder, room, settings.Value);
        }
    }
}
