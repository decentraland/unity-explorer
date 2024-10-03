using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using ECS;
using Global.AppArgs;
using System.Threading;
using Utility;
using PlayerMovementNetSendSystem = DCL.Multiplayer.Movement.Systems.PlayerMovementNetSendSystem;
using RemotePlayersMovementSystem = DCL.Multiplayer.Movement.Systems.RemotePlayersMovementSystem;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private const string COMPRESSION_ARG_FLAG = "compression";
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RemoteEntities remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly ProvidedAsset<MultiplayerDebugSettings> debugSettings;
        private readonly bool useCompression;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRealmData realmData;
        private readonly IRemoteMetadata remoteMetadata;

        private ProvidedAsset<MultiplayerMovementSettings> settings;

        private Entity? selfReplicaEntity;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, MultiplayerMovementMessageBus messageBus, IDebugContainerBuilder debugBuilder
          , RemoteEntities remoteEntities, ExposedTransform playerTransform, ProvidedAsset<MultiplayerDebugSettings> debugSettings, IAppArgs appArgs,
            IReadOnlyEntityParticipantTable entityParticipantTable, IRealmData realmData, IRemoteMetadata remoteMetadata)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.debugSettings = debugSettings;
            this.entityParticipantTable = entityParticipantTable;
            this.realmData = realmData;
            this.remoteMetadata = remoteMetadata;
            this.useCompression = appArgs.TryGetValue(COMPRESSION_ARG_FLAG, out string? compression) && compression == "true";
        }

        public void Dispose()
        {
            messageBus.Dispose();
            settings.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.spatialStateSettings, ct);
            this.settings.Value.UseCompression = useCompression;
            messageBus.InitializeEncoder(this.settings.Value.EncodingSettings, this.settings.Value);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, messageBus, settings.Value, debugSettings.Value);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, settings.Value, settings.Value.CharacterControllerSettings);
            RemotePlayerAnimationSystem.InjectToWorld(ref builder, settings.Value.ExtrapolationSettings);
            CleanUpRemoteMotionSystem.InjectToWorld(ref builder);
            MultiplayerMovementDebugSystem.InjectToWorld(ref builder, arguments.PlayerEntity, realmData, debugBuilder, remoteEntities, playerTransform, debugSettings.Value, settings.Value, entityParticipantTable, remoteMetadata);
        }
    }
}
