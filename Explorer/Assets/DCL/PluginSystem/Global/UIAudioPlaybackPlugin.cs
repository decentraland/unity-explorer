using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class UIAudioPlaybackPlugin : IDCLGlobalPlugin<UIAudioPlaybackPlugin.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioPlaybackController? uiAudioManagerContainer;

        public UIAudioPlaybackPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
            if (uiAudioManagerContainer != null) { uiAudioManagerContainer.Dispose(); }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioManagerContainer = (await assetsProvisioner.ProvideInstanceAsync(settings.AudioManagerContainerReference, ct: ct)).Value;
            uiAudioManagerContainer.Initialize();
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(UIAudioPlaybackPlugin) + "." + nameof(AudioPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public AudioManagerContainerReference AudioManagerContainerReference;
        }

        [Serializable]
        public class AudioManagerContainerReference : ComponentReference<UIAudioPlaybackController>
        {
            public AudioManagerContainerReference(string guid) : base(guid) { }
        }
    }
}
