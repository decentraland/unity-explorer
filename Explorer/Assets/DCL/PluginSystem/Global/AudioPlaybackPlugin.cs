using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Audio.Systems;
using DCL.Landscape;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AudioPlaybackPlugin : IDCLGlobalPlugin<AudioPlaybackPlugin.PluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly bool enableLandscape;

#if !UNITY_WEBGL
        private readonly TerrainGenerator terrainGenerator;
        private readonly WorldTerrainGenerator worldTerrainGenerator;
#endif

        private readonly AudioMixerVolumesController mixerVolumesController;
        private readonly IRealmData realmData;

        private ProvidedInstance<UIAudioPlaybackController> uiAudioPlaybackController;
        private ProvidedInstance<WorldAudioPlaybackController> worldAudioPlaybackController;
        private ProvidedAsset<LandscapeAudioSystemSettings> landscapeAudioSettings;

        public AudioPlaybackPlugin(

#if !UNITY_WEBGL
                TerrainGenerator terrainGenerator,
                WorldTerrainGenerator worldTerrainGenerator,
#endif

                IAssetsProvisioner assetsProvisioner,
                bool enableLandscape,
                AudioMixerVolumesController mixerVolumesController,
                IRealmData realmData
                )
        {

#if !UNITY_WEBGL
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;
#endif

            this.assetsProvisioner = assetsProvisioner;
            this.enableLandscape = enableLandscape;
            this.mixerVolumesController = mixerVolumesController;
            this.realmData = realmData;
        }

        public void Dispose()
        {
            uiAudioPlaybackController.Dispose();
            worldAudioPlaybackController.Dispose();
            landscapeAudioSettings.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {

#if !UNITY_WEBGL
            if (enableLandscape)
                LandscapeAudioCullingSystem.InjectToWorld(ref builder, terrainGenerator, worldTerrainGenerator, landscapeAudioSettings.Value, worldAudioPlaybackController.Value, realmData);
#endif

        }

        public async UniTask InitializeAsync(PluginSettings settings, CancellationToken ct)
        {
            uiAudioPlaybackController = await assetsProvisioner.ProvideInstanceAsync(settings.UIAudioPlaybackControllerReference, ct: ct);
            worldAudioPlaybackController = await assetsProvisioner.ProvideInstanceAsync(settings.WorldAudioPlaybackControllerReference, ct: ct);
            landscapeAudioSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeAudioSettingsReference, ct: ct);

            uiAudioPlaybackController.Value.Initialize(mixerVolumesController);
            worldAudioPlaybackController.Value.Initialize();
        }

        [Serializable]
        public class PluginSettings : IDCLPluginSettings
        {
            [field: SerializeField] public LandscapeAudioSettingsReference LandscapeAudioSettingsReference { get; private set; }
            [field: SerializeField] public UIAudioPlaybackControllerReference UIAudioPlaybackControllerReference { get; private set; }
            [field: SerializeField] public WorldAudioPlaybackControllerReference WorldAudioPlaybackControllerReference { get; private set; }
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

        [Serializable]
        public class LandscapeAudioSettingsReference : AssetReferenceT<LandscapeAudioSystemSettings>
        {
            public LandscapeAudioSettingsReference(string guid) : base(guid) { }
        }
    }
}
