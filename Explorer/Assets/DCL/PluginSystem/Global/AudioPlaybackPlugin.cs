using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using System;
using System.Threading;
using UnityEngine;
using AudioSettings = DCL.Audio.AudioSettings;

namespace DCL.PluginSystem.Global
{
    public class AudioPlaybackPlugin : IDCLGlobalPlugin<AudioPlaybackPlugin.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private AudioGeneralController audioGeneralController;

        public AudioPlaybackPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
            if (audioGeneralController != null) {audioGeneralController.Dispose();}
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            audioGeneralController = (await assetsProvisioner.ProvideInstanceAsync(settings.AudioGeneralControllerReference, ct: ct)).Value;
            audioGeneralController.Initialize();
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AudioPlaybackPlugin) + "." + nameof(AudioPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public AudioGeneralControllerReference AudioGeneralControllerReference;
        }


        [Serializable]
        public class AudioGeneralControllerReference : ComponentReference<AudioGeneralController>
        {
            public AudioGeneralControllerReference(string guid) : base(guid) { }
        }

    }
}
