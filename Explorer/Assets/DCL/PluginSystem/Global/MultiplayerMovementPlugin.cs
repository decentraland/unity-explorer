using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Platforms;
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
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RemoteEntities remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly MultiplayerDebugSettings debugSettings;
        private readonly IAppArgs appArgs;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRealmData realmData;
        private readonly IRemoteMetadata remoteMetadata;

        private MultiplayerMovementSettings settings;

        private Entity? selfReplicaEntity;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, MultiplayerMovementMessageBus messageBus, IDebugContainerBuilder debugBuilder
          , RemoteEntities remoteEntities, ExposedTransform playerTransform, MultiplayerDebugSettings debugSettings, IAppArgs appArgs,
            IReadOnlyEntityParticipantTable entityParticipantTable, IRealmData realmData, IRemoteMetadata remoteMetadata)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.debugSettings = debugSettings;
            this.appArgs = appArgs;
            this.entityParticipantTable = entityParticipantTable;
            this.realmData = realmData;
            this.remoteMetadata = remoteMetadata;
        }

        public void Dispose()
        {
            messageBus.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = settings.MovementSettings;

            ConfigureCompressionUsage();

            messageBus.InitializeEncoder(this.settings.EncodingSettings, this.settings, (await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeData, ct)).Value);
        }

        private void ConfigureCompressionUsage()
        {
            if (appArgs.TryGetValue(AppArgsFlags.Multiplayer.COMPRESSION, out string? compression))
            {
                this.settings.UseCompression = compression == "true";
                return;
            }

            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                settings.UseCompression = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_WIN);
            else if (IPlatform.DEFAULT.Is(IPlatform.Kind.Mac))
                settings.UseCompression = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_MAC);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, messageBus, settings, debugSettings);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, settings, settings.CharacterControllerSettings);
            RemotePlayerAnimationSystem.InjectToWorld(ref builder, settings.ExtrapolationSettings, settings);
            CleanUpRemoteMotionSystem.InjectToWorld(ref builder);
            MultiplayerMovementDebugSystem.InjectToWorld(ref builder, arguments.PlayerEntity, realmData, debugBuilder, remoteEntities, playerTransform, debugSettings, settings, entityParticipantTable, remoteMetadata);
        }
    }
}
