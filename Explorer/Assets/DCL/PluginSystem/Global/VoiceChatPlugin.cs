using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Settings.Settings;
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

        private VoiceChatMicrophoneHandler voiceChatHandler;

        public VoiceChatPlugin(ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy, IAssetsProvisioner assetsProvisioner, DCLInput dclInput, IRoomHub roomHub)
        {
            this.voiceChatSettingsProxy = voiceChatSettingsProxy;
            this.assetsProvisioner = assetsProvisioner;
            this.dclInput = dclInput;
            this.roomHub = roomHub;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ProvidedInstance<VoiceChatMicrophoneAudioFilter> microphoneAudioFilter = await assetsProvisioner.ProvideInstanceAsync(settings.MicrophoneAudioFilter, ct: ct);
            var microphoneAudioSource = microphoneAudioFilter.Value.GetComponent<AudioSource>();

            ProvidedAsset<VoiceChatSettingsAsset> voiceChatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatSettings, ct: ct);
            var voiceChatSettings = voiceChatSettingsAsset.Value;
            voiceChatSettingsProxy.SetObject(voiceChatSettings);
            
            microphoneAudioFilter.Value.Initialize(voiceChatSettings);
            
            voiceChatHandler = new VoiceChatMicrophoneHandler(dclInput, voiceChatSettings, microphoneAudioSource, microphoneAudioFilter.Value);

            ProvidedInstance<VoiceChatCombinedAudioSource> audioSource = await assetsProvisioner.ProvideInstanceAsync(settings.CombinedAudioSource, ct: ct);

            var livekitRoomHandler = new VoiceChatLivekitRoomHandler(audioSource.Value, microphoneAudioFilter.Value, microphoneAudioSource, roomHub.VoiceChatRoom());
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
            public class MicrophoneAudioFilterReference : ComponentReference<VoiceChatMicrophoneAudioFilter>
            {
                public MicrophoneAudioFilterReference(string guid) : base(guid) { }
            }
        }
    }
}
