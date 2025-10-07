using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.UI.Profiles.Helpers;
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
        private readonly IDebugContainerBuilder debugContainer;
        private readonly IRoomHub roomHub;
        private readonly VoiceChatPanelView voiceChatPanelView;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IWebRequestController webRequestController;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatTrackManager? trackManager;
        private VoiceChatRoomManager? roomManager;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private MicrophoneAudioToggleHandler? microphoneAudioToggleHandler;
        private VoiceChatPanelPresenter? voiceChatPanelController;
        private VoiceChatDebugContainer? voiceChatDebugContainer;

        public VoiceChatPlugin(
            IRoomHub roomHub,
            VoiceChatPanelView voiceChatPanelView,
            VoiceChatContainer voiceChatContainer,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            IAssetsProvisioner assetsProvisioner,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            IDebugContainerBuilder debugContainer)
        {
            this.roomHub = roomHub;
            this.voiceChatPanelView = voiceChatPanelView;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.communityDataProvider = communityDataProvider;
            this.webRequestController = webRequestController;
            this.assetsProvisioner = assetsProvisioner;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.debugContainer = debugContainer;
            voiceChatOrchestrator = voiceChatContainer.VoiceChatOrchestrator;
        }

        public void Dispose()
        {
            if (voiceChatHandler == null || roomManager == null)
            {
                // Attempted to dispose before initialization - this is expected in some scenarios
                return;
            }

            voiceChatPanelController?.Dispose();
            voiceChatPluginSettingsAsset.Dispose();
            microphoneStateManager?.Dispose();
            nametagsHandler?.Dispose();
            voiceChatHandler.Dispose();
            roomManager?.Dispose();
            microphoneAudioToggleHandler?.Dispose();
            voiceChatDebugContainer?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatPluginSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);
            VoiceChatPluginSettings pluginSettings = voiceChatPluginSettingsAsset.Value;

            VoiceChatConfiguration voiceChatConfiguration = pluginSettings.VoiceChatConfiguration;

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration, voiceChatOrchestrator);
            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);

            trackManager = new VoiceChatTrackManager(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, voiceChatHandler);
            roomManager = new VoiceChatRoomManager(trackManager, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator,
                entityParticipantTable,
                world,
                playerEntity);

            VoiceChatParticipantEntryView playerEntry = pluginSettings.PlayerEntryView;
            AudioClipConfig muteMicrophoneAudio = pluginSettings.MuteMicrophoneAudio;
            AudioClipConfig unmuteMicrophoneAudio = pluginSettings.UnmuteMicrophoneAudio;
            microphoneAudioToggleHandler = new MicrophoneAudioToggleHandler(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);

            voiceChatPanelController = new VoiceChatPanelPresenter(voiceChatPanelView, profileDataProvider, communityDataProvider, webRequestController, voiceChatOrchestrator, voiceChatHandler, roomManager, roomHub, playerEntry, chatSharedAreaEventBus);

            voiceChatDebugContainer = new VoiceChatDebugContainer(debugContainer, trackManager);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatConfigurationsReference VoiceChatConfigurations { get; private set; } = null!;

            [Serializable]
            public class VoiceChatConfigurationsReference : AssetReferenceT<VoiceChatPluginSettings>
            {
                public VoiceChatConfigurationsReference(string guid) : base(guid) { }
            }
        }
    }
}
