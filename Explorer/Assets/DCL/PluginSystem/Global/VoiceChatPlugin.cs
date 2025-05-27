using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Settings.Settings;
using DCL.UI.MainUI;
using DCL.Utilities;
using DCL.VoiceChat;
using LiveKit;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        private readonly ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly DCLInput dclInput;
        private readonly IRoomHub roomHub;
        private readonly MainUIView mainUIView;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        public VoiceChatPlugin(
            ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy,
            IAssetsProvisioner assetsProvisioner,
            DCLInput dclInput,
            IRoomHub roomHub,
            MainUIView mainUIView,
            IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.voiceChatSettingsProxy = voiceChatSettingsProxy;
            this.assetsProvisioner = assetsProvisioner;
            this.dclInput = dclInput;
            this.roomHub = roomHub;
            this.mainUIView = mainUIView;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ProvidedInstance<AudioFilter> microphoneAudioFilter = await assetsProvisioner.ProvideInstanceAsync(settings.MicrophoneAudioFilter, ct: ct);
            var microphoneAudioSource = microphoneAudioFilter.Value.GetComponent<AudioSource>();

            ProvidedAsset<VoiceChatSettingsAsset> voiceChatSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatSettings, ct: ct);
            voiceChatSettingsProxy.SetObject(voiceChatSettings.Value);
            var voiceChatHandler = new VoiceChatMicrophoneHandler(dclInput, voiceChatSettings.Value, microphoneAudioSource);

            ProvidedInstance<VoiceChatCombinedAudioSource> audioSource = await assetsProvisioner.ProvideInstanceAsync(settings.CombinedAudioSource, ct: ct);

            var livekitRoomHandler = new VoiceChatLivekitRoomHandler(audioSource.Value, microphoneAudioFilter.Value, microphoneAudioSource, roomHub.VoiceChatRoom());

            VoiceChatController controller = new VoiceChatController(mainUIView.VoiceChatView, voiceChatCallStatusService, voiceChatHandler);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatSettingsReference VoiceChatSettings { get; private set; }
            [field: SerializeField] public MicrophoneAudioFilterReference MicrophoneAudioFilter { get; private set; }
            [field: SerializeField] public CombinedAudioSourceReference CombinedAudioSource { get; private set; }

            [Serializable]
            public class VoiceChatSettingsReference : AssetReferenceT<VoiceChatSettingsAsset>
            {
                public VoiceChatSettingsReference(string guid) : base(guid) { }
            }

            [Serializable]
            public class CombinedAudioSourceReference : ComponentReference<VoiceChatCombinedAudioSource>
            {
                public CombinedAudioSourceReference(string guid) : base(guid) { }
            }

            [Serializable]
            public class MicrophoneAudioFilterReference : ComponentReference<AudioFilter>
            {
                public MicrophoneAudioFilterReference(string guid) : base(guid) { }
            }
        }
    }
}
