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
        public TerrainGenerator TerrainGenerator;
        public WorldTerrainGenerator WorldTerrainGenerator;

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly bool enableLandscape;
        public ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<ParcelData> parcelData;
        private NativeArray<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner, IDebugContainerBuilder debugContainerBuilder, MapRendererTextureContainer textureContainer, bool enableLandscape)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = enableLandscape;
            TerrainGenerator = null!;
            WorldTerrainGenerator = null!;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            if (!enableLandscape) return;

            parcelData = await assetsProvisioner.ProvideMainAssetAsync(settings.parsedParcels, ct);

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);

            emptyParcels = parcelData.Value.GetEmptyParcels();
            ownedParcels = parcelData.Value.GetOwnedParcels();

            TerrainGenerator = new TerrainGenerator(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels);
            WorldTerrainGenerator = new WorldTerrainGenerator(landscapeData.Value.terrainData, ref ownedParcels);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, landscapeData.Value, textureContainer);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, realmPartitionSettings.Value, landscapeData.Value);
            LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, TerrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, TerrainGenerator);
        }

        public void Dispose()
        {
            if (!enableLandscape) return;
            TerrainGenerator.Dispose();
        }

        public async UniTask InitializeLoadingProgressAsync(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await TerrainGenerator.GenerateTerrainAsync(processReport: loadReport, cancellationToken: ct);

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }
}
