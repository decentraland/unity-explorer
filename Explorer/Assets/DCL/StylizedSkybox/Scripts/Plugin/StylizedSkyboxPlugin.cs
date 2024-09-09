using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.StylizedSkybox.Scripts.Plugin
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private SkyboxController? skyboxController;
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

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settings.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settings.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(settings.SkyboxMaterial, directionalLight, skyboxAnimation);

            debugContainerBuilder.TryAddWidget("Skybox")
                                 ?.AddSingleButton("Play", () => skyboxController.Play())
                                 .AddSingleButton("Pause", () => skyboxController.Pause())
                                 .AddIntSliderField("Time", timeOfDay, 0, skyboxController.SecondsInDay)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTime(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
            public Material SkyboxMaterial = null!;
            public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

            [Serializable]
            public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
            {
                public StylizedSkyboxControllerRef(string guid) : base(guid) { }
            }
        }
    }
}
