using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Entities;
using System.Threading;
using Utility;
using PlayerMovementNetSendSystem = DCL.Multiplayer.Movement.Systems.PlayerMovementNetSendSystem;
using RemotePlayersMovementSystem = DCL.Multiplayer.Movement.Systems.RemotePlayersMovementSystem;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RemoteEntities remoteEntities;
        private readonly ExposedTransform playerTransform;

        private ProvidedAsset<MultiplayerMovementSettings> settings;
        private Entity? selfReplicaEntity;
        private MultiplayerMovementDebug multiplayerMovementDebug;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, MultiplayerMovementMessageBus messageBus, IDebugContainerBuilder debugBuilder
          , RemoteEntities remoteEntities, ExposedTransform playerTransform)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
        }

        public void Dispose()
        {
            multiplayerMovementDebug.Dispose();
            messageBus.Dispose();
            settings.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.spatialStateSettings, ct);
            messageBus.InitializeEncoder(this.settings.Value.EncodingSettings);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, messageBus, settings.Value);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, settings.Value, settings.Value.CharacterControllerSettings);
            RemotePlayerAnimationSystem.InjectToWorld(ref builder, settings.Value.ExtrapolationSettings);
            CleanUpRemoteMotionSystem.InjectToWorld(ref builder);

            multiplayerMovementDebug = new MultiplayerMovementDebug(builder.World, arguments.PlayerEntity, debugBuilder, remoteEntities, playerTransform, settings);
        }
    }
}
