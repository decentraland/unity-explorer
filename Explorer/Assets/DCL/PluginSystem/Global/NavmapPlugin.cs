using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Navmap;
using MVC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class NavmapPlugin : IDCLGlobalPlugin<NavmapPlugin.NavmapSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly NavmapView navmapView;

        public NavmapPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, NavmapView navmapView)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.navmapView = navmapView;
        }

        public UniTask InitializeAsync(NavmapSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }


        public class NavmapSettings : IDCLPluginSettings
        {
            [field: Header(nameof(NavmapPlugin) + "." + nameof(NavmapSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject explorePanelPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject persistentExploreOpenerPrefab;
        }
    }
}
