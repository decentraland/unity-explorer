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
        private TerrainGenerator terrainGenerator = null!;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private readonly MapRendererTextureContainer textureContainer;
        private ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<ParcelData> parcelData;
        private NativeArray<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner, IDebugContainerBuilder debugContainerBuilder, MapRendererTextureContainer textureContainer)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            parcelData = await assetsProvisioner.ProvideMainAssetAsync(settings.parsedParcels, ct);

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            emptyParcels = parcelData.Value.GetEmptyParcels();
            ownedParcels = parcelData.Value.GetOwnedParcels();

            terrainGenerator = new TerrainGenerator(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, realmPartitionSettings.Value, landscapeData.Value);
            LandscapeViewSystem.InjectToWorld(ref builder, landscapeData.Value, textureContainer, terrainGenerator);
        }

        public void Dispose()
        {
            terrainGenerator.Dispose();
        }

        public async UniTask InitializeLoadingProgressAsync(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await terrainGenerator.GenerateTerrainAsync(processReport: loadReport, cancellationToken: ct);

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }
}
