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

namespace DCL.PluginSystem.Global
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner
        )
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {  

        }

        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            [field: Header(nameof(StylizedSkyboxPlugin) + "." + nameof(StylizedSkyboxSettings))]
            [field: Space]
            [field: SerializeField]
            public StylizedSkyboxControllerRef StylizedSkyboxController;
            public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
            {

                public StylizedSkyboxControllerRef(string guid) : base(guid) { }
            }
        }


    }
}

