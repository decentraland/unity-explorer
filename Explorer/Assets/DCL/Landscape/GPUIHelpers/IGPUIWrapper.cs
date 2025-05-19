using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Settings;

public interface IGPUIWrapper
{
    void Setup(TerrainGenerator terrainGenerator, ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder);
    UniTask InitializeAsync(IAssetsProvisioner assetsProvisioner, GPUIAssets.GPUIAssetsRef settingsGpuiAssetsRef, CancellationToken ct);
    void OverrideTerrainGenerationData(TerrainGenerationData valueTerrainData);
}