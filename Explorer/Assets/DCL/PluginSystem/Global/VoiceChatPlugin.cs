using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Settings.Settings;
using DCL.UI.MainUI;
using DCL.Utilities;
using DCL.VoiceChat;
using MVC;
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
        private readonly ViewDependencies dependencies;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatConfigurations;
        private ProvidedInstance<VoiceChatMicrophoneAudioFilter> microphoneAudioFilter;
        private ProvidedAsset<VoiceChatSettingsAsset> voiceChatSettingsAsset;
        private ProvidedAsset<VoiceChatConfiguration> voiceChatConfigurationAsset;
        private ProvidedInstance<VoiceChatCombinedAudioSource> audioSource;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatLivekitRoomHandler? livekitRoomHandler;
        private VoiceChatController controller;

        public VoiceChatPlugin(
            ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy,
            IAssetsProvisioner assetsProvisioner,
            DCLInput dclInput,
            IRoomHub roomHub,
            MainUIView mainUIView,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            ViewDependencies dependencies)
        {
            this.voiceChatSettingsProxy = voiceChatSettingsProxy;
            this.assetsProvisioner = assetsProvisioner;
            this.dclInput = dclInput;
            this.roomHub = roomHub;
            this.mainUIView = mainUIView;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.dependencies = dependencies;
        }

        public void Dispose()
        {
            if (voiceChatHandler == null || livekitRoomHandler == null)
            {
                // Attempted to dispose before initialization - this is expected in some scenarios
                return;
            }

            voiceChatHandler.Dispose();
            livekitRoomHandler.Dispose();

            audioSource.Dispose();
            voiceChatConfigurationAsset.Dispose();
            voiceChatSettingsAsset.Dispose();
            microphoneAudioFilter.Dispose();
            voiceChatConfigurations.Dispose();
            controller.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            voiceChatConfigurations = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);
            VoiceChatPluginSettings configurations = voiceChatConfigurations.Value;

            microphoneAudioFilter = await assetsProvisioner.ProvideInstanceAsync(configurations.MicrophoneAudioFilter, ct: ct);
            AudioSource? microphoneAudioSource = microphoneAudioFilter.Value.GetComponent<AudioSource>();

            voiceChatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatSettings, ct: ct);
            VoiceChatSettingsAsset voiceChatSettings = voiceChatSettingsAsset.Value;
            voiceChatSettingsProxy.SetObject(voiceChatSettings);

            voiceChatConfigurationAsset = await assetsProvisioner.ProvideMainAssetAsync(configurations.VoiceChatConfiguration, ct: ct);
            VoiceChatConfiguration voiceChatConfiguration = voiceChatConfigurationAsset.Value;

            microphoneAudioFilter.Value.Initialize(voiceChatConfiguration);
            audioSource = await assetsProvisioner.ProvideInstanceAsync(configurations.CombinedAudioSource, ct: ct);

            voiceChatHandler = new VoiceChatMicrophoneHandler(dclInput, voiceChatSettings, voiceChatConfiguration, microphoneAudioSource, microphoneAudioFilter.Value, voiceChatCallStatusService);

            livekitRoomHandler = new VoiceChatLivekitRoomHandler(audioSource.Value, microphoneAudioFilter.Value, microphoneAudioSource, roomHub.VoiceChatRoom().Room(), voiceChatCallStatusService, roomHub, voiceChatHandler);

            controller = new VoiceChatController(mainUIView.VoiceChatView, voiceChatCallStatusService, voiceChatHandler, dependencies);
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
