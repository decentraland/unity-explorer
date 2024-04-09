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
    public class UIAudioPlaybackPlugin : IDCLGlobalPlugin<UIAudioPlaybackPlugin.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioPlaybackController UIAudioPlaybackController;

        public UIAudioPlaybackPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
            if (UIAudioPlaybackController != null) {UIAudioPlaybackController.Dispose();}
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            UIAudioPlaybackController = (await assetsProvisioner.ProvideInstanceAsync(settings.UIAudioPlaybackControllerReference, ct: ct)).Value;
            UIAudioPlaybackController.Initialize();
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(UIAudioPlaybackPlugin) + "." + nameof(AudioPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public UIAudioPlaybackControllerReference UIAudioPlaybackControllerReference;
        }


        [Serializable]
        public class UIAudioPlaybackControllerReference : ComponentReference<UIAudioPlaybackController>
        {
            public UIAudioPlaybackControllerReference(string guid) : base(guid) { }
        }

    }
}
