using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Settings.Settings;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.VoiceChat.Permissions;
using DCL.WebRequests;
using LiveKit.Runtime.Scripts.Audio;
using RustAudio;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly MainUIView mainUIView;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
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
            IRoomHub roomHub,
            MainUIView mainUIView,
            VoiceChatContainer voiceChatContainer,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            IAssetsProvisioner assetsProvisioner,
            IDebugContainerBuilder debugContainer)
        {
            this.roomHub = roomHub;
            this.mainUIView = mainUIView;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.communityDataProvider = communityDataProvider;
            this.webRequestController = webRequestController;
            this.assetsProvisioner = assetsProvisioner;
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

            voiceChatPluginSettingsAsset.Dispose();
            microphoneStateManager?.Dispose();
            nametagsHandler?.Dispose();
            voiceChatHandler.Dispose();
            roomManager?.Dispose();
            microphoneAudioToggleController?.Dispose();
            privateVoiceChatController?.Dispose();
            communitiesVoiceChatController?.Dispose();
            sceneVoiceChatController?.Dispose();
            voiceChatPanelResizeController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatPluginSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);
            var pluginSettings = this.voiceChatPluginSettingsAsset.Value;

            VoiceChatConfiguration voiceChatConfiguration = pluginSettings.VoiceChatConfiguration;

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration);
            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);

            trackManager = new VoiceChatTrackManager(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, voiceChatHandler);
            roomManager = new VoiceChatRoomManager(trackManager, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator,
                entityParticipantTable,
                world,
                playerEntity);

            var playerEntry = pluginSettings.PlayerEntryView;
            var muteMicrophoneAudio = pluginSettings.MuteMicrophoneAudio;
            var unmuteMicrophoneAudio = pluginSettings.UnmuteMicrophoneAudio;

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(mainUIView.VoiceChatPanelResizeView, voiceChatOrchestrator);

            microphoneAudioToggleController = new MicrophoneAudioToggleController(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);

            privateVoiceChatController = new PrivateVoiceChatController(mainUIView.VoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatController(mainUIView.CommunityVoiceChatView, playerEntry, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(mainUIView.SceneVoiceChatTitlebarView, voiceChatOrchestrator);

            var availableMicrophones = new ElementBinding<ulong>(0);
            var currentMicrophone = new ElementBinding<string>(string.Empty);
            var permissionsStatus = new ElementBinding<string>(string.Empty);
            var sourceIndex = new ElementBinding<ulong>(0);
            var sampleRate = new ElementBinding<ulong>(0);
            var channels = new ElementBinding<ulong>(0);

            debugContainer.TryAddWidget(IDebugContainerBuilder.Categories.MICROPHONE)
                         ?.AddMarker("Available Microphones", availableMicrophones, DebugLongMarkerDef.Unit.NoFormat)
                          .AddCustomMarker("Permission Status", permissionsStatus)
                          .AddCustomMarker("Current Microphone", currentMicrophone)
                          .AddMarker("Source Index", sourceIndex, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Sample Rate", sampleRate, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Channels", channels, DebugLongMarkerDef.Unit.NoFormat)
                          .AddSingleButton("Update", UpdateWidget);

            void UpdateWidget()
            {
                availableMicrophones.Value = (ulong)MicrophoneSelection.Devices().Length;
                currentMicrophone.Value = VoiceChatSettings.SelectedMicrophone?.name ?? string.Empty;

                var activeSources = RustAudioSource.Info.ActiveSources();

                if (activeSources.Count > 1)
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "Active sources are not expected to be more than 1 per time, Explorer must use single microphone");

                KeyValuePair<ulong, RustAudioSource>? source = activeSources.Count > 0 ? activeSources.First() : null;
                sourceIndex.Value = source?.Key ?? 0;
                sampleRate.Value = source?.Value?.sampleRate ?? 0;
                channels.Value = source?.Value?.channels ?? 0;

#if UNITY_STANDALONE_OSX
                permissionsStatus.Value = VoiceChatPermissions.CurrentState().ToString()!;
#else
                permissionsStatus.Value = "Mac ONLY";
#endif
            }
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
