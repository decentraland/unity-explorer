using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.PluginSystem.Global
{
    public class UIAudioPlugin : IDCLGlobalPlugin<UIAudioPlugin.UIAudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioManagerContainer uiAudioManagerContainer;

        public UIAudioPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
            uiAudioManagerContainer.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(UIAudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioManagerContainer = (await assetsProvisioner.ProvideInstanceAsync(settings.UIAudioManagerContainerReference, ct: ct)).Value;
            uiAudioManagerContainer.Initialize();
        }

        public class UIAudioPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(UIAudioPlugin) + "." + nameof(UIAudioPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public UIAudioManagerContainerReference UIAudioManagerContainerReference;
        }

        [Serializable]
        public class UIAudioManagerContainerReference : ComponentReference<UIAudioManagerContainer>
        {
            public UIAudioManagerContainerReference(string guid) : base(guid) { }
        }
    }
}
