using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class UIAudioPlugin : IDCLGlobalPlugin<UIAudioPlugin.UIAudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IUIAudioEventsBus audioEventsBus;
        private UIAudioManagerContainer uiAudioManagerContainer;

        public UIAudioPlugin(IAssetsProvisioner assetsProvisioner, IUIAudioEventsBus audioEventsBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.audioEventsBus = audioEventsBus;
        }

        public void Dispose()
        {
            uiAudioManagerContainer.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(UIAudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioManagerContainer = (await assetsProvisioner.ProvideInstanceAsync<UIAudioManagerContainer>(settings.UIAudioManagerContainerReference, ct: ct)).Value;
            this.uiAudioManagerContainer.Setup(audioEventsBus);
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
