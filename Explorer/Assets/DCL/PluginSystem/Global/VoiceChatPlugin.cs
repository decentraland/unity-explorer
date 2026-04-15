using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.Systems;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms;
using System;
using System.Collections.Concurrent;
using System.Threading;
using DCL.UI;
using DCL.Utilities.Extensions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DCL.FeatureFlags;
using DCL.Utilities;
using Utility;
using AudioSettings = UnityEngine.AudioSettings;
using RustAudio;

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
        private readonly ImageControllerProvider  imageControllerProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;
        private readonly EventSubscriptionScope pluginScope = new ();
        private readonly ConcurrentDictionary<string, LivekitAudioSource> nearbyAudioSources = new ();
        private readonly NearbyMuteService? nearbyMuteService;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private MicrophoneTrackPublisher? microphonePublisher;
        private RemoteTrackListener? remoteListener;
        private VoiceChatRoomManager? roomManager;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private MicrophoneAudioToggleHandler? microphoneAudioToggleHandler;
        private VoiceChatPanelPresenter? voiceChatPanelPresenter;
        private VoiceChatDebugContainer? voiceChatDebugContainer;
        private NearbyVoiceChatManager? nearbyVoiceChatManager;
        private NearbyVoiceChatStateModel? nearbyStateModel;
        private VoiceChatNametagsHandler? nearbyNametagsHandler;
        private VoiceChatConfiguration voiceChatConfiguration;

        public VoiceChatPlugin(
            IRoomHub roomHub,
            VoiceChatPanelView voiceChatPanelView,
            VoiceChatContainer voiceChatContainer,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity,
            CommunitiesDataProvider communityDataProvider,
            ImageControllerProvider imageControllerProvider,
            IAssetsProvisioner assetsProvisioner,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            IDebugContainerBuilder debugContainer,
            NearbyMuteService? nearbyMuteService = null)
        {
            this.nearbyMuteService = nearbyMuteService;
            this.roomHub = roomHub;
            this.voiceChatPanelView = voiceChatPanelView;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.communityDataProvider = communityDataProvider;
            this.imageControllerProvider = imageControllerProvider;
            this.assetsProvisioner = assetsProvisioner;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.debugContainer = debugContainer;

            voiceChatOrchestrator = voiceChatContainer.VoiceChatOrchestrator;
        }

        public void Dispose()
        {
            pluginScope.Dispose();

            if (voiceChatPluginSettingsAsset.Value != null)
                voiceChatPluginSettingsAsset.Dispose();

            RustAudioClient.DeInit();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                NearbyAudioPositionSystem.InjectToWorld(ref builder, entityParticipantTable, nearbyAudioSources);
                NearbyAudioDebugSystem.InjectToWorld(ref builder, voiceChatConfiguration, debugContainer);
            }
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "VOICE CHAT!");
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatPluginSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);

            VoiceChatPluginSettings pluginSettings = voiceChatPluginSettingsAsset.Value;
            voiceChatConfiguration = pluginSettings.VoiceChatConfiguration;

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration, voiceChatOrchestrator);
            pluginScope.Add(voiceChatHandler);

            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);
            pluginScope.Add(microphoneStateManager);

            microphonePublisher = new MicrophoneTrackPublisher(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, voiceChatHandler, VoiceChatType.COMMUNITY);
            remoteListener = new RemoteTrackListener(
                roomHub.VoiceChatRoom().Room(),
                voiceChatConfiguration,
                new PlaybackSourcesHub("Call", voiceChatConfiguration.ChatAudioMixerGroup.EnsureNotNull(), false));

            roomManager = new VoiceChatRoomManager(microphonePublisher, remoteListener, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager);
            pluginScope.Add(roomManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator.CurrentCallStatus.Select(MapCallStatus),
                entityParticipantTable,
                world,
                playerEntity);
            pluginScope.Add(nametagsHandler);

            VoiceChatParticipantEntryView playerEntry = pluginSettings.PlayerEntryView;
            AudioClipConfig muteMicrophoneAudio = pluginSettings.MuteMicrophoneAudio;
            AudioClipConfig unmuteMicrophoneAudio = pluginSettings.UnmuteMicrophoneAudio;
            microphoneAudioToggleHandler = new MicrophoneAudioToggleHandler(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);
            pluginScope.Add(microphoneAudioToggleHandler);

            voiceChatPanelPresenter = new VoiceChatPanelPresenter(voiceChatPanelView, profileDataProvider, communityDataProvider, imageControllerProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, roomHub, playerEntry, chatSharedAreaEventBus);
            pluginScope.Add(voiceChatPanelPresenter);

            voiceChatDebugContainer = new VoiceChatDebugContainer(debugContainer, microphonePublisher, remoteListener);
            pluginScope.Add(voiceChatDebugContainer);

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                IRoom islandRoom = roomHub.IslandRoom();

                nearbyStateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
                pluginScope.Add(nearbyStateModel);

                voiceChatHandler.SetNearbyStateModel(nearbyStateModel);

                if (nearbyMuteService != null)
                    await nearbyMuteService.LoadAsync(ct);

                nearbyVoiceChatManager = new NearbyVoiceChatManager(
                    islandRoom, voiceChatConfiguration,
                    nearbyAudioSources, voiceChatOrchestrator.CurrentCallStatus,
                    nearbyStateModel, voiceChatHandler, nearbyMuteService);
                pluginScope.Add(nearbyVoiceChatManager);

                nearbyNametagsHandler = new VoiceChatNametagsHandler(
                    islandRoom,
                    nearbyStateModel.State.Select(MapNearbyState),
                    entityParticipantTable,
                    world,
                    playerEntity);
                pluginScope.Add(nearbyNametagsHandler);
            }
        }

        private static VoiceChatActivityState MapCallStatus(VoiceChatStatus status) =>
            status switch
            {
                VoiceChatStatus.VOICE_CHAT_IN_CALL => VoiceChatActivityState.ACTIVE,
                VoiceChatStatus.VOICE_CHAT_ENDING_CALL
                    or VoiceChatStatus.DISCONNECTED
                    or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR => VoiceChatActivityState.INACTIVE,
                _ => VoiceChatActivityState.TRANSITION,
            };

        private static VoiceChatActivityState MapNearbyState(NearbyVoiceChatState state) =>
            state switch
            {
                NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING => VoiceChatActivityState.ACTIVE,
                _ => VoiceChatActivityState.INACTIVE,
            };

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
