using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.Landscape.Utils;
using DCL.MapRenderer.ComponentsFactory;
using DCL.RealmNavigation;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using LandscapeDebugSystem = DCL.Landscape.Systems.LandscapeDebugSystem;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLGlobalPlugin<LandscapeSettings>
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly WorldTerrainGenerator worldTerrainGenerator;

        private readonly IRealmData realmData;
        private readonly ILoadingStatus loadingStatus;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly bool enableLandscape;
        private readonly bool isZone;
        private readonly LandscapeParcelService parcelService;

        private RealmPartitionSettingsAsset realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<ParcelData> parcelData;
        private NativeList<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;
        private SatelliteFloor? floor;

        // private IGPUIWrapper gpuiWrapper;

        public LandscapePlugin(IRealmData realmData,
            ILoadingStatus loadingStatus,
            IScenesCache sceneCache,
            TerrainGenerator terrainGenerator,
            WorldTerrainGenerator worldTerrainGenerator,
            IAssetsProvisioner assetsProvisioner,
            IDebugContainerBuilder debugContainerBuilder,
            MapRendererTextureContainer textureContainer,
            IWebRequestController webRequestController,
            bool enableLandscape,
            bool isZone)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = enableLandscape;
            this.isZone = isZone;
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;

            parcelService = new LandscapeParcelService(webRequestController, isZone);

            // gpuiWrapper = new GPUIWrapper();
        }

        public void Dispose()
        {
            if (enableLandscape)
            {
                terrainGenerator.Dispose();
                worldTerrainGenerator.Dispose();
            }
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            floor = new SatelliteFloor(realmData, landscapeData.Value);

            if (!enableLandscape) return;

            parcelData = await assetsProvisioner.ProvideMainAssetAsync(settings.parsedParcels, ct);

            realmPartitionSettings = settings.realmPartitionSettings;

            FetchParcelResult fetchParcelResult = await parcelService.LoadManifestAsync(ct);

            if (!fetchParcelResult.Succeeded)
            {
                emptyParcels = parcelData.Value.GetEmptyParcels();
                ownedParcels = parcelData.Value.GetOwnedParcels();
            }
            else
            {
                emptyParcels = fetchParcelResult.Manifest.GetEmptyParcels();
                ownedParcels = fetchParcelResult.Manifest.GetOwnedParcels();
            }

            // gpuiWrapper.SetupLandscapeData(landscapeData.Value);

            terrainGenerator.Initialize(landscapeData.Value.terrainData, landscapeData.Value.TreesProfile,
                ref emptyParcels, ref ownedParcels);

            worldTerrainGenerator.Initialize(landscapeData.Value.worldsTerrainData, new CPUTerrainDetailSetter());
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, floor);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, floor, realmPartitionSettings, landscapeData.Value);
            //LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            //LandscapeCollidersCullingSystem.InjectToWorld(ref builder, terrainGenerator, scenesCache, loadingStatus);
            RenderGroundSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            CollideTerrainSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);

            // gpuiWrapper.InjectDebugSystem(ref builder, debugContainerBuilder);
        }
    }
}
