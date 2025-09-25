using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Parcel;
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
        private readonly LandscapeParcelData landscapeParcelData;
        private readonly LandscapeParcelController landscapeParcelController;

        private RealmPartitionSettingsAsset realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeData;
        private NativeParallelHashSet<int2> emptyParcels;
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
            LandscapeParcelData landscapeParcelData,
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
            this.landscapeParcelData = landscapeParcelData;

            landscapeParcelController = new LandscapeParcelController(
                assetsProvisioner,
                new LandscapeParcelService(webRequestController, isZone),
                landscapeParcelData
            );

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

            realmPartitionSettings = settings.realmPartitionSettings;

            await landscapeParcelController.InitializeAsync(settings.parsedParcels, ct);

            var emptyParcelsRef = landscapeParcelData.EmptyParcels;
            var ownedParcelsRef = landscapeParcelData.OccupiedParcels;

            // gpuiWrapper.SetupLandscapeData(landscapeData.Value);

            terrainGenerator.Initialize(landscapeData.Value.terrainData, landscapeData.Value.TreesProfile,
                ref emptyParcelsRef, ref ownedParcelsRef);

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
