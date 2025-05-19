using System;
using System.Collections.Generic;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape.Settings;

namespace DCL.Landscape.GPUIHelpers
{
    public class FakeGPUIWrapper : IGPUIWrapper
    {
        public void Setup(TerrainGenerator terrainGenerator, ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder)
        {
        }

        public UniTask InitializeAsync(IAssetsProvisioner assetsProvisioner, GPUIAssets.GPUIAssetsRef settingsGpuiAssetsRef, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        //If its the fake implementation, nothing to override
        public void OverrideTerrainGenerationData(TerrainGenerationData valueTerrainData) { }
    }
}