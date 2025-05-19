using System;
using System.Collections.Generic;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.GPUI_Pro;
using DCL.Landscape.Settings;
using GPUInstancerPro;
using GPUInstancerPro.TerrainModule;
using UnityEngine;
using Object = UnityEngine.Object;

public class GPUIWrapper : IGPUIWrapper
{
    private GPUIAssets gpuiAsset;

    private GPUITreeManager treeManager;
    private GPUIDetailManager detailManager;
    private GPUIDebuggerCanvas debuggerCanvasPrefab;


    public void Setup(TerrainGenerator terrainGenerator, ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder)
    {
        GPUIDebugSystem.InjectToWorld(ref worldBuilder, debugBuilder, debuggerCanvasPrefab);
        terrainGenerator.GenesisTerrainGenerated += GenesisTerrainGenerated;

        void GenesisTerrainGenerated(List<Terrain> generatedTerrain)
        {
            foreach (var terrain in generatedTerrain)
            {
                GPUITerrainAPI.AddTerrain(treeManager, terrain);
                GPUITerrainAPI.AddTerrain(detailManager, terrain);
            }
        }
    }

    public async UniTask InitializeAsync(IAssetsProvisioner assetsProvisioner, GPUIAssets.GPUIAssetsRef settingsGpuiAssetsRef, CancellationToken ct)
    {
        var gpuiAssetData = await assetsProvisioner.ProvideMainAssetAsync(settingsGpuiAssetsRef, ct);

        gpuiAsset = gpuiAssetData.Value;
        treeManager = Object.Instantiate(gpuiAsset.treeManagerPrefab);
        detailManager = Object.Instantiate(gpuiAsset.detailsManagerPrefab);
        debuggerCanvasPrefab = gpuiAssetData.Value.debuggerCanvasPrefab;
    }

    public void OverrideTerrainGenerationData(TerrainGenerationData valueTerrainData)
    {
        valueTerrainData.treeAssets = gpuiAsset.treesToOverride;
        valueTerrainData.detailAssets = gpuiAsset.detailToOverride;
    }
}