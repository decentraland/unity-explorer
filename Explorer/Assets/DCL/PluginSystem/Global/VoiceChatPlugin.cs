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

        private ProvidedInstance<VoiceChatMicrophoneAudioFilter> microphoneAudioFilter;
        private ProvidedInstance<VoiceChatCombinedStreamsAudioSource> combinedAudioSource;
        private VoiceChatSettingsAsset voiceChatSettings;
        private VoiceChatConfiguration voiceChatConfiguration;
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
            microphoneAudioFilter.Dispose();
            controller?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            microphoneAudioFilter = await assetsProvisioner.ProvideInstanceAsync(settings.MicrophoneAudioFilter, ct: ct);
            AudioSource? microphoneAudioSource = microphoneAudioFilter.Value.GetComponent<AudioSource>();

            voiceChatSettings = settings.VoiceChatSettings;
            voiceChatConfiguration = settings.VoiceChatConfiguration;

            microphoneAudioFilter.Value.Initialize(voiceChatConfiguration);
            combinedAudioSource = await assetsProvisioner.ProvideInstanceAsync(settings.CombinedAudioSource, ct: ct);

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatSettings, voiceChatConfiguration, microphoneAudioSource, microphoneAudioFilter.Value);
            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatCallStatusService);

            livekitRoomHandler = new VoiceChatLivekitRoomHandler(combinedAudioSource.Value, voiceChatHandler, roomHub.VoiceChatRoom().Room(), voiceChatCallStatusService, roomHub, voiceChatConfiguration, microphoneStateManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatCallStatusService,
                entityParticipantTable,
                world,
                playerEntity);

            controller = new VoiceChatController(mainUIView.VoiceChatView, voiceChatCallStatusService, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());

        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatSettingsAsset VoiceChatSettings { get; private set; }
            [field: SerializeField] public MicrophoneAudioFilterReference MicrophoneAudioFilter { get; private set; }
            [field: SerializeField] public CombinedAudioSourceReference CombinedAudioSource { get; private set; }
            [field: SerializeField] public VoiceChatConfiguration VoiceChatConfiguration { get; private set; }

            [Serializable]
            public class CombinedAudioSourceReference : ComponentReference<VoiceChatCombinedStreamsAudioSource>
            {
                public CombinedAudioSourceReference(string guid) : base(guid) { }
            }

            [Serializable]
            public class MicrophoneAudioFilterReference : ComponentReference<VoiceChatMicrophoneAudioFilter>
            {
                public MicrophoneAudioFilterReference(string guid) : base(guid) { }
            }
        }
    }
}
