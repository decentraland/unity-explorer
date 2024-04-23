using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Optimization.Pools;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class AudioPlaybackContainer : DCLContainer<AudioPlaybackContainer.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioPlaybackController uiAudioPlaybackController;
        private WorldAudioPlaybackController worldAudioPlaybackController;

        public AudioPlaybackContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public static async UniTask<(AudioPlaybackContainer? container, bool success)> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            IAssetsProvisioner assetsProvisioner,
            CancellationToken ct)
        {
            var container = new AudioPlaybackContainer(assetsProvisioner);
            return await container.InitializeContainerAsync<AudioPlaybackContainer, AudioPluginSettings>(settingsContainer, ct, c => UniTask.CompletedTask);
        }

        public override void Dispose()
        {
            uiAudioPlaybackController.Dispose();
            worldAudioPlaybackController.Dispose();
        }

        protected override async UniTask InitializeInternalAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioPlaybackController = (await assetsProvisioner.ProvideInstanceAsync(settings.UIAudioPlaybackControllerReference, ct: ct)).Value;
            worldAudioPlaybackController = (await assetsProvisioner.ProvideInstanceAsync(settings.WorldAudioPlaybackControllerReference, ct: ct)).Value;

            uiAudioPlaybackController.Initialize();
            worldAudioPlaybackController.Initialize();
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Space]
            [field: SerializeField]
            public UIAudioPlaybackControllerReference UIAudioPlaybackControllerReference { get; private set; }
            [field: Space]
            [field: SerializeField]
            public WorldAudioPlaybackControllerReference WorldAudioPlaybackControllerReference { get; private set; }

        }


        [Serializable]
        public class UIAudioPlaybackControllerReference : ComponentReference<UIAudioPlaybackController>
        {
            public UIAudioPlaybackControllerReference(string guid) : base(guid) { }
        }
        [Serializable]
        public class WorldAudioPlaybackControllerReference : ComponentReference<WorldAudioPlaybackController>
        {
            public WorldAudioPlaybackControllerReference(string guid) : base(guid) { }
        }

    }
}
