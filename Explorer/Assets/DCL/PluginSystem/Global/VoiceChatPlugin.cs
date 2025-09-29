using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;
using DCL.Utility.Types;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.VoiceChat.Permissions;
using DCL.WebRequests;
using LiveKit.Audio;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RustAudio;
using System;
using System.Collections.Generic;
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

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatTrackManager? trackManager;
        private VoiceChatRoomManager? roomManager;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private MicrophoneAudioToggleController? microphoneAudioToggleController;
        private VoiceChatPanelController? voiceChatPanelController;

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
            microphoneAudioToggleController?.Dispose();
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
            microphoneAudioToggleController = new MicrophoneAudioToggleController(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);

            voiceChatPanelController = new VoiceChatPanelController(voiceChatPanelView, profileDataProvider, communityDataProvider, webRequestController, voiceChatOrchestrator, voiceChatHandler, roomManager, roomHub, playerEntry);

            var availableMicrophones = new ElementBinding<ulong>(0);
            var currentMicrophone = new ElementBinding<string>(string.Empty);
#if UNITY_STANDALONE_OSX
            var permissionsStatus = new ElementBinding<string>(string.Empty);
#endif
            var isRecording = new ElementBinding<string>(string.Empty);
            var sampleRate = new ElementBinding<ulong>(0);
            var channels = new ElementBinding<ulong>(0);

            var remoteSpeakers = new ElementBinding<ulong>(0);
            var speakersInfo = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());

            List<StreamInfo<AudioStreamInfo>> infoBuffer = new ();
            List<(string name, string value)> speakersBuffer = new ();

            CancellationTokenSource? autoUpdateCts = null;

            debugContainer.TryAddWidget(IDebugContainerBuilder.Categories.MICROPHONE)
                         ?.AddMarker("Available Microphones", availableMicrophones, DebugLongMarkerDef.Unit.NoFormat)
#if UNITY_STANDALONE_OSX
                          .AddCustomMarker("Permission Status", permissionsStatus)
#endif
                          .AddCustomMarker("Current Microphone", currentMicrophone)
                          .AddCustomMarker("Is Recording", isRecording)
                          .AddMarker("Sample Rate", sampleRate, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Channels", channels, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Remote Speakers", remoteSpeakers, DebugLongMarkerDef.Unit.NoFormat)
                          .AddList("Speakers Info", speakersInfo)
                          .AddToggleField("Auto Update", v => AutoUpdateTriggerAsync(v.newValue).Forget(), false)
                          .AddSingleButton("Update", UpdateWidget);

            return;

            async UniTaskVoid AutoUpdateTriggerAsync(bool enable)
            {
                if (enable)
                {
                    autoUpdateCts = new CancellationTokenSource();
                    CancellationToken current = autoUpdateCts.Token;
                    TimeSpan pollDelay = TimeSpan.FromMilliseconds(500);

                    while (current.IsCancellationRequested == false)
                    {
                        bool cancelled = await UniTask.Delay(pollDelay, cancellationToken: current).SuppressCancellationThrow();
                        if (cancelled) return;

                        UpdateWidget();
                    }
                }
                else
                {
                    autoUpdateCts?.Cancel();
                    autoUpdateCts?.Dispose();
                    autoUpdateCts = null;
                }
            }

            void UpdateWidget()
            {
                availableMicrophones.Value = (ulong)MicrophoneSelection.Devices().Length;
                currentMicrophone.Value = VoiceChatSettings.SelectedMicrophone?.name ?? string.Empty;

                var currentMicrophoneOption = trackManager.CurrentMicrophone.Resource;

                MicrophoneInfo info = currentMicrophoneOption.Has
                    ? currentMicrophoneOption.Value.MicrophoneInfo
                    : default(MicrophoneInfo);

                isRecording.Value = (currentMicrophoneOption.Has && currentMicrophoneOption.Value.IsRecording).ToString();
                sampleRate.Value = info.sampleRate;
                channels.Value = info.channels;

#if UNITY_STANDALONE_OSX
                permissionsStatus.Value = VoiceChatPermissions.CurrentState().ToString()!;
#endif

                trackManager.ActiveStreamsInfo(infoBuffer);
                remoteSpeakers.Value = (ulong)infoBuffer.Count;

                speakersBuffer.Clear();
                foreach (StreamInfo<AudioStreamInfo> streamInfo in infoBuffer)
                {
                    speakersBuffer.Add((streamInfo.key.identity, $"SampleRate - {streamInfo.info.sampleRate}"));
                    speakersBuffer.Add((streamInfo.key.identity, $"Channels - {streamInfo.info.numChannels}"));
                }

                speakersInfo.SetAndUpdate(speakersBuffer);
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
