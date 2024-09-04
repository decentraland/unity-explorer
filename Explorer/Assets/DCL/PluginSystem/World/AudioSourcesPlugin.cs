using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace DCL.PluginSystem.World
{
    public class AudioSourcesPlugin: IDCLWorldPlugin<AudioSourcesPlugin.AudioSourcesPluginSettings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;
        private readonly IAssetsProvisioner assetsProvisioner;
        private AudioMixer audioMixer;

        internal readonly AudioClipsCache audioClipsCache;

        public AudioSourcesPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController, CacheCleaner cacheCleaner, IAssetsProvisioner assetsProvisioner)
        {
            this.webRequestController = webRequestController;

            frameTimeBudgetProvider = sharedDependencies.FrameTimeBudget;
            memoryBudgetProvider = sharedDependencies.MemoryBudget;

            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<AudioSource>(onRelease: audioSource => audioSource.clip = null);
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<AudioSource>());

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);

            this.assetsProvisioner = assetsProvisioner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            StartAudioSourceLoadingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, frameTimeBudgetProvider);
            LoadAudioClipSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController);
            sceneIsCurrentListeners.Add(UpdateAudioSourceSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, audioClipsCache, componentPoolsRegistry, frameTimeBudgetProvider, memoryBudgetProvider, audioMixer, sharedDependencies.SceneStateProvider));

            finalizeWorldSystems.Add(CleanUpAudioSourceSystem.InjectToWorld(ref builder, audioClipsCache, componentPoolsRegistry));
        }

        public void Dispose()
        { }

        public async UniTask InitializeAsync(AudioSourcesPluginSettings settings, CancellationToken ct) =>
            audioMixer = (await assetsProvisioner.ProvideMainAssetAsync(settings.GeneralAudioMixer, ct)).Value;

        [Serializable]
        public class AudioSourcesPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AudioSourcesPlugin) + "." + nameof(AudioSourcesPluginSettings))]
            [field: Space]
            [field: SerializeField] internal AssetReferenceT<AudioMixer> GeneralAudioMixer;
        }

    }
}
