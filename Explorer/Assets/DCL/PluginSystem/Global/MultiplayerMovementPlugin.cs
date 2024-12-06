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
using ECS;
using Global.AppArgs;
using System.Threading;
using Utility;
using PlayerMovementNetSendSystem = DCL.Multiplayer.Movement.Systems.PlayerMovementNetSendSystem;
using RemotePlayersMovementSystem = DCL.Multiplayer.Movement.Systems.RemotePlayersMovementSystem;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerMovementPlugin : IDCLGlobalPlugin<MultiplayerCommunicationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RemoteEntities remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly ProvidedAsset<MultiplayerDebugSettings> debugSettings;
        private readonly IAppArgs appArgs;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRealmData realmData;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly FeatureFlagsCache featureFlagsCache;

        private ProvidedAsset<MultiplayerMovementSettings> settings;

        private Entity? selfReplicaEntity;

        public MultiplayerMovementPlugin(IAssetsProvisioner assetsProvisioner, MultiplayerMovementMessageBus messageBus, IDebugContainerBuilder debugBuilder
          , RemoteEntities remoteEntities, ExposedTransform playerTransform, ProvidedAsset<MultiplayerDebugSettings> debugSettings, IAppArgs appArgs,
            IReadOnlyEntityParticipantTable entityParticipantTable, IRealmData realmData, IRemoteMetadata remoteMetadata, FeatureFlagsCache featureFlagsCache)
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
            this.featureFlagsCache = featureFlagsCache;
        }

        public void Dispose()
        {
            messageBus.Dispose();
            settings.Dispose();
        }

        public async UniTask InitializeAsync(MultiplayerCommunicationSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.spatialStateSettings, ct);

            ConfigureCompressionUsage();

            messageBus.InitializeEncoder(this.settings.Value.EncodingSettings, this.settings.Value);
        }

        private void ConfigureCompressionUsage()
        {
            if (appArgs.TryGetValue(AppArgsFlags.Multiplayer.COMPRESSION, out string? compression))
            {
                this.settings.Value.UseCompression = compression == "true";
                return;
            }

            string os = SystemInfo.operatingSystem;
            if (os.Contains("Windows"))
                this.settings.Value.UseCompression = featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_WIN);
            else if (os.Contains("Mac"))
                this.settings.Value.UseCompression = featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_MAC);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerMovementNetSendSystem.InjectToWorld(ref builder, messageBus, settings.Value, debugSettings.Value);
            RemotePlayersMovementSystem.InjectToWorld(ref builder, settings.Value, settings.Value.CharacterControllerSettings);
            RemotePlayerAnimationSystem.InjectToWorld(ref builder, settings.Value.ExtrapolationSettings, settings.Value);
            CleanUpRemoteMotionSystem.InjectToWorld(ref builder);
            MultiplayerMovementDebugSystem.InjectToWorld(ref builder, arguments.PlayerEntity, realmData, debugBuilder, remoteEntities, playerTransform, debugSettings.Value, settings.Value, entityParticipantTable, remoteMetadata);
        }
    }
}
