using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Systems.Debug;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Platforms;
using ECS;
using Global.AppArgs;
using System.Threading;
using Utility;
using CleanUpRemoteMotionSystem = DCL.Multiplayer.Movement.CleanUpRemoteMotionSystem;
using MultiplayerMovementDebugSystem = DCL.Multiplayer.Movement.MultiplayerMovementDebugSystem;
using PlayerMovementNetSendSystem = DCL.Multiplayer.Movement.PlayerMovementNetSendSystem;
using RemotePlayerAnimationSystem = DCL.Multiplayer.Movement.RemotePlayerAnimationSystem;
using RemotePlayersMovementSystem = DCL.Multiplayer.Movement.RemotePlayersMovementSystem;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly LiveKitMovementMessageBus liveKitMovementMessageBus;
        private readonly PulseMultiplayerBus pulseMultiplayerBus;
        private readonly IMovementMessageBus effectiveMovementMessageBus;
        private readonly IPulseMultiplayerService pulseMultiplayerService;
        private readonly ITransport pulseTransport;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RemoteEntities remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly MultiplayerDebugSettings debugSettings;
        private readonly IAppArgs appArgs;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRealmData realmData;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly ParcelEncoder parcelEncoder;

        private MultiplayerMovementSettings settings;
        private Entity? selfReplicaEntity;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner,
            LiveKitMovementMessageBus liveKitMovementMessageBus,
            PulseMultiplayerBus pulseMultiplayerBus,
            IMovementMessageBus effectiveMovementMessageBus,
            IPulseMultiplayerService pulseMultiplayerService, ITransport pulseTransport,
            IDebugContainerBuilder debugBuilder,
            RemoteEntities remoteEntities, ExposedTransform playerTransform, MultiplayerDebugSettings debugSettings, IAppArgs appArgs,
            IReadOnlyEntityParticipantTable entityParticipantTable, IRealmData realmData, IRemoteMetadata remoteMetadata, ParcelEncoder parcelEncoder)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.liveKitMovementMessageBus = liveKitMovementMessageBus;
            this.pulseMultiplayerBus = pulseMultiplayerBus;
            this.effectiveMovementMessageBus = effectiveMovementMessageBus;
            this.pulseMultiplayerService = pulseMultiplayerService;
            this.pulseTransport = pulseTransport;
            this.debugBuilder = debugBuilder;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.debugSettings = debugSettings;
            this.appArgs = appArgs;
            this.entityParticipantTable = entityParticipantTable;
            this.realmData = realmData;
            this.remoteMetadata = remoteMetadata;
            this.parcelEncoder = parcelEncoder;
        }

        public void Dispose()
        {
            liveKitMovementMessageBus.Dispose();
        }

        public UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = settings.MovementSettings;

            ConfigureCompressionUsage();

            liveKitMovementMessageBus.InitializeEncoder(this.settings.EncodingSettings, this.settings, parcelEncoder);
            return UniTask.CompletedTask;
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
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, liveKitMovementMessageBus, effectiveMovementMessageBus, settings, debugSettings);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, settings, settings.CharacterControllerSettings);
            RemotePlayerAnimationSystem.InjectToWorld(ref builder, settings.ExtrapolationSettings, settings);
            CleanUpRemoteMotionSystem.InjectToWorld(ref builder);
            MultiplayerMovementDebugSystem.InjectToWorld(ref builder, arguments.PlayerEntity, realmData, debugBuilder, remoteEntities, playerTransform, debugSettings, settings, entityParticipantTable, remoteMetadata);
            DebugPulseSystem.InjectToWorld(ref builder, pulseMultiplayerService, pulseTransport, pulseMultiplayerBus, debugBuilder);
        }
    }
}
