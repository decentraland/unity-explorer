using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class UIAudioPlaybackContainer : DCLContainer<UIAudioPlaybackContainer.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioPlaybackController uiAudioPlaybackController;

        public UIAudioPlaybackContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public static async UniTask<(UIAudioPlaybackContainer? container, bool success)> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            IAssetsProvisioner assetsProvisioner,
            CancellationToken ct)
        {
            var container = new UIAudioPlaybackContainer(assetsProvisioner);
            return await container.InitializeContainerAsync<UIAudioPlaybackContainer, AudioPluginSettings>(settingsContainer, ct, c => UniTask.CompletedTask);
        }

        public override void Dispose()
        {
            uiAudioPlaybackController.Dispose();
        }

        protected override async UniTask InitializeInternalAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioPlaybackController = (await assetsProvisioner.ProvideInstanceAsync(settings.UIAudioPlaybackControllerReference, ct: ct)).Value;
            uiAudioPlaybackController.Initialize();
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Space]
            [field: SerializeField]
            public UIAudioPlaybackControllerReference UIAudioPlaybackControllerReference { get; private set; }
        }

        [Serializable]
        public class UIAudioPlaybackControllerReference : ComponentReference<UIAudioPlaybackController>
        {
            public UIAudioPlaybackControllerReference(string guid) : base(guid) { }
        }
    }
}
