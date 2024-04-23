using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AsyncLoadReporting;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Interface;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Prioritization;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using LandscapeDebugSystem = DCL.Landscape.Systems.LandscapeDebugSystem;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLGlobalPlugin<LandscapeSettings>, ILandscapeInitialization
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly WorldTerrainGenerator worldTerrainGenerator;

        private readonly SatelliteView view;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly bool enableLandscape;
        private ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<ParcelData> parcelData;
        private NativeList<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;

        public LandscapePlugin(SatelliteView view, TerrainGenerator terrainGenerator, WorldTerrainGenerator worldTerrainGenerator, IAssetsProvisioner assetsProvisioner, IDebugContainerBuilder debugContainerBuilder, MapRendererTextureContainer textureContainer, bool enableLandscape)
        {
            this.view = view;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = enableLandscape;

            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            if (!enableLandscape) return;

            parcelData = await assetsProvisioner.ProvideMainAssetAsync(settings.parsedParcels, ct);

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);

            emptyParcels = parcelData.Value.GetEmptyParcels();
            ownedParcels = parcelData.Value.GetOwnedParcels();

            terrainGenerator.Initialize(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels);
            worldTerrainGenerator.Initialize(landscapeData.Value.worldsTerrainData);
            view.Initialize(landscapeData.Value);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, view);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, view, realmPartitionSettings.Value, landscapeData.Value);
            LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
        }

        public void Dispose()
        {
            if (enableLandscape)
            {
                terrainGenerator.Dispose();
                worldTerrainGenerator.Dispose();
            }
        }

        public async UniTask InitializeLoadingProgressAsync(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await terrainGenerator.GenerateTerrainAsync(processReport: loadReport, cancellationToken: ct);

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }
}
