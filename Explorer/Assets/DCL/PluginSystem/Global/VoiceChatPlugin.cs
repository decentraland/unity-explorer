using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Communities;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Settings.Settings;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using AudioSettings = UnityEngine.AudioSettings;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRoomHub roomHub;
        private readonly MainUIView mainUIView;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly PlayerParcelTrackerService playerParcelTracker;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatConfigurations;
        private ProvidedAsset<VoiceChatSettingsAsset> voiceChatSettingsAsset;
        private ProvidedAsset<VoiceChatConfiguration> voiceChatConfigurationAsset;
        private ProvidedInstance<VoiceChatCombinedStreamsAudioSource> combinedAudioSource;
        private ProvidedAsset<PlayerEntryView> playerEntry;
        private ProvidedAsset<AudioClipConfig> muteMicrophoneAudio;
        private ProvidedAsset<AudioClipConfig> unmuteMicrophoneAudio;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatTrackManager? trackManager;
        private VoiceChatRoomManager? roomManager;
        private PrivateVoiceChatController? privateVoiceChatController;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private CommunityVoiceChatController? communitiesVoiceChatController;
        private VoiceChatPanelResizeController? voiceChatPanelResizeController;
        private MicrophoneAudioToggleController? microphoneAudioToggleController;
        private SceneVoiceChatController? sceneVoiceChatController;

        public VoiceChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IRoomHub roomHub,
            MainUIView mainUIView,
            VoiceChatContainer voiceChatContainer,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            PlayerParcelTrackerService playerParcelTracker
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.roomHub = roomHub;
            this.mainUIView = mainUIView;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.communityDataProvider = communityDataProvider;
            this.webRequestController = webRequestController;
            this.playerParcelTracker = playerParcelTracker;
            voiceChatOrchestrator = voiceChatContainer.VoiceChatOrchestrator;
            communityVoiceChatCallStatusService = voiceChatContainer.CommunityVoiceChatCallStatusService;
        }

        public void Dispose()
        {
            if (voiceChatHandler == null || roomManager == null)
            {
                // Attempted to dispose before initialization - this is expected in some scenarios
                return;
            }

            microphoneStateManager?.Dispose();
            nametagsHandler?.Dispose();
            voiceChatHandler.Dispose();
            roomManager?.Dispose();
            microphoneAudioToggleController?.Dispose();

            combinedAudioSource.Dispose();
            voiceChatConfigurationAsset.Dispose();
            voiceChatSettingsAsset.Dispose();
            voiceChatConfigurations.Dispose();
            muteMicrophoneAudio.Dispose();
            unmuteMicrophoneAudio.Dispose();
            privateVoiceChatController?.Dispose();
            communitiesVoiceChatController?.Dispose();
            voiceChatOrchestrator?.Dispose();
            voiceChatPanelResizeController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatConfigurations = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);
            VoiceChatPluginSettings configurations = voiceChatConfigurations.Value;

            voiceChatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatSettings, ct: ct);
            VoiceChatSettingsAsset voiceChatSettings = voiceChatSettingsAsset.Value;

            voiceChatConfigurationAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatConfiguration, ct: ct);
            VoiceChatConfiguration voiceChatConfiguration = voiceChatConfigurationAsset.Value;

            combinedAudioSource = await assetsProvisioner.ProvideInstanceAsync(configurations.CombinedAudioSource, ct: ct);

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatSettings, voiceChatConfiguration);
            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);

            trackManager = new VoiceChatTrackManager(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, combinedAudioSource.Value, voiceChatHandler);
            roomManager = new VoiceChatRoomManager(trackManager, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator,
                entityParticipantTable,
                world,
                playerEntity);

            playerEntry = await assetsProvisioner.ProvideMainAssetAsync(configurations.PlayerEntryView, ct: ct);
            muteMicrophoneAudio = await assetsProvisioner.ProvideMainAssetAsync(configurations.MuteMicrophoneAudio, ct: ct);
            unmuteMicrophoneAudio = await assetsProvisioner.ProvideMainAssetAsync(configurations.UnmuteMicrophoneAudio, ct: ct);

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(mainUIView.VoiceChatPanelResizeView, voiceChatOrchestrator);

            microphoneAudioToggleController = new MicrophoneAudioToggleController(voiceChatHandler, muteMicrophoneAudio.Value, unmuteMicrophoneAudio.Value);

            privateVoiceChatController = new PrivateVoiceChatController(mainUIView.VoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatController(mainUIView.CommunityVoiceChatView, playerEntry.Value, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(mainUIView.SceneVoiceChatTitlebarView, communityDataProvider, voiceChatOrchestrator);
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
