using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;
using DCL.DebugUtilities.UIBindings;

namespace DCL.PluginSystem.Global
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private SkyboxController skyboxController;
        private readonly ElementBinding<int> timeOfDay;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder
        )
        {
            timeOfDay = new ElementBinding<int>(0);
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settings.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            Material skyboxMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.SkyboxMaterial, ct: ct)).Value;
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settings.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(skyboxMaterial, directionalLight, skyboxAnimation);

            debugContainerBuilder.AddWidget("Skybox")
                                 .AddSingleButton("Play", () => skyboxController.Play())
                                 .AddSingleButton("Pause", () => skyboxController.Pause())
                                 .AddIntSliderField("Time", timeOfDay, 0, skyboxController.SecondsInDay)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTime(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            [field: Header(nameof(StylizedSkyboxPlugin) + "." + nameof(StylizedSkyboxSettings))]
            [field: Space]
            [field: SerializeField]
            public StylizedSkyboxControllerRef StylizedSkyboxPrefab;

            [field: SerializeField]
            public AssetReferenceMaterial SkyboxMaterial;

            [field: SerializeField]
            public AssetReferenceT<AnimationClip> SkyboxAnimationCycle;

            [Serializable]
            public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
            {
                public StylizedSkyboxControllerRef(string guid) : base(guid) { }
            }
        }
    }
}
