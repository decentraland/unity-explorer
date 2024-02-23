using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.DebugUtilities;
using MVC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private SkyboxController skyboxController;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;

            //debugContainerBuilder.AddWidget("Skybox");
            //.AddToggleField("enable skybox", OnEnableSkybox, true);
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settings.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            Material skyboxMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.SkyboxMaterial, ct: ct)).Value;
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settings.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(skyboxMaterial, directionalLight, skyboxAnimation);
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

