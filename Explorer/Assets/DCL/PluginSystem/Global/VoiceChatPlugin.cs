using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Settings.Settings;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRoomHub roomHub;
        private readonly MainUIView mainUIView;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatConfigurations;
        private ProvidedInstance<VoiceChatMicrophoneAudioFilter> microphoneAudioFilter;
        private ProvidedAsset<VoiceChatSettingsAsset> voiceChatSettingsAsset;
        private ProvidedAsset<VoiceChatConfiguration> voiceChatConfigurationAsset;
        private ProvidedInstance<VoiceChatCombinedStreamsAudioSource> combinedAudioSource;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatLivekitRoomHandler? livekitRoomHandler;
        private VoiceChatController? controller;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;

        public VoiceChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IRoomHub roomHub,
            MainUIView mainUIView,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.roomHub = roomHub;
            this.mainUIView = mainUIView;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            if (voiceChatHandler == null || livekitRoomHandler == null)
            {
                // Attempted to dispose before initialization - this is expected in some scenarios
                return;
            }

            microphoneStateManager?.Dispose();
            nametagsHandler?.Dispose();
            voiceChatHandler.Dispose();
            livekitRoomHandler.Dispose();

            combinedAudioSource.Dispose();
            voiceChatConfigurationAsset.Dispose();
            voiceChatSettingsAsset.Dispose();
            microphoneAudioFilter.Dispose();
            voiceChatConfigurations.Dispose();
            controller?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatConfigurations = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);
            VoiceChatPluginSettings configurations = voiceChatConfigurations.Value;

            microphoneAudioFilter = await assetsProvisioner.ProvideInstanceAsync(configurations.MicrophoneAudioFilter, ct: ct);
            AudioSource? microphoneAudioSource = microphoneAudioFilter.Value.GetComponent<AudioSource>();

            voiceChatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatSettings, ct: ct);
            VoiceChatSettingsAsset voiceChatSettings = voiceChatSettingsAsset.Value;

            voiceChatConfigurationAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatConfiguration, ct: ct);
            VoiceChatConfiguration voiceChatConfiguration = voiceChatConfigurationAsset.Value;

            microphoneAudioFilter.Value.Initialize(voiceChatConfiguration);
            combinedAudioSource = await assetsProvisioner.ProvideInstanceAsync(configurations.CombinedAudioSource, ct: ct);

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatSettings, voiceChatConfiguration, microphoneAudioSource, microphoneAudioFilter.Value);
            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatCallStatusService);

            livekitRoomHandler = new VoiceChatLivekitRoomHandler(combinedAudioSource.Value, voiceChatHandler, roomHub.VoiceChatRoom().Room(), voiceChatCallStatusService, roomHub, voiceChatConfiguration, microphoneStateManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatCallStatusService,
                entityParticipantTable,
                world,
                playerEntity);

            controller = new VoiceChatController(
                mainUIView.VoiceChatView,
                voiceChatCallStatusService,
                voiceChatHandler,
                profileDataProvider,
                roomHub.VoiceChatRoom());

        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatConfigurationsReference VoiceChatConfigurations { get; private set; }

            [Serializable]
            public class VoiceChatConfigurationsReference : AssetReferenceT<VoiceChatPluginSettings>
            {
                public VoiceChatConfigurationsReference(string guid) : base(guid) { }
            }
        }
    }
}
