using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.PluginSystem.Global
{
    public class AudioPlaybackPlugin : IDCLGlobalPlugin<AudioPlaybackPlugin.AudioPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private UIAudioManagerContainer uiAudioManagerContainer;
        private IComponentPool<AudioSource> audioSourcePool;

        public AudioPlaybackPlugin(IAssetsProvisioner assetsProvisioner, IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;

            audioSourcePool = componentPoolsRegistry.GetReferenceTypePool<AudioSource>();

            if (audioSourcePool == null)
            {
                componentPoolsRegistry.AddGameObjectPool<AudioSource>(onRelease: audioSource => audioSource.clip = null);
                audioSourcePool = componentPoolsRegistry.GetReferenceTypePool<AudioSource>();
            }

            cacheCleaner.Register(audioSourcePool);

        }

        public void Dispose()
        {
            uiAudioManagerContainer.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(AudioPluginSettings settings, CancellationToken ct)
        {
            uiAudioManagerContainer = (await assetsProvisioner.ProvideInstanceAsync(settings.AudioManagerContainerReference, ct: ct)).Value;
            uiAudioManagerContainer.Initialize(audioSourcePool);
        }

        public class AudioPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AudioPlaybackPlugin) + "." + nameof(AudioPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public AudioManagerContainerReference AudioManagerContainerReference;
        }

        [Serializable]
        public class AudioManagerContainerReference : ComponentReference<UIAudioManagerContainer>
        {
            public AudioManagerContainerReference(string guid) : base(guid) { }
        }
    }
}
