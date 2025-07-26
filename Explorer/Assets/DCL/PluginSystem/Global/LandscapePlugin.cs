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
using DCL.Prefs;
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
        private readonly IScenesCache scenesCache;

        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<ParcelData> parcelData;
        private NativeList<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;
        private SatelliteFloor? floor;

        private IGPUIWrapper gpuiWrapper;
        private readonly bool isGPUIEnabledFF;

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
            bool isZone,
            bool isGpuiEnabledFf)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.scenesCache = sceneCache;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = false;
            isGPUIEnabledFF = isGpuiEnabledFf;
            this.isZone = isZone;
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;

            parcelService = new LandscapeParcelService(webRequestController, isZone);

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

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);

            FetchParcelResult fetchParcelResult = await parcelService.LoadManifestAsync(ct);
            string parcelChecksum = string.Empty;

            if (!fetchParcelResult.Succeeded)
            {
                emptyParcels = parcelData.Value.GetEmptyParcels();
                ownedParcels = parcelData.Value.GetOwnedParcels();
            }
            else
            {
                emptyParcels = fetchParcelResult.Manifest.GetEmptyParcels();
                ownedParcels = fetchParcelResult.Manifest.GetOwnedParcels();
                parcelChecksum = fetchParcelResult.Checksum;
            }

            CheckGPUIFF();
            gpuiWrapper.SetupLandscapeData(landscapeData.Value);
            terrainGenerator.Initialize(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels,
                parcelChecksum, isZone, gpuiWrapper, gpuiWrapper.GetDetailSetter());

            worldTerrainGenerator.Initialize(landscapeData.Value.worldsTerrainData, new CPUTerrainDetailSetter());
        }

        private void CheckGPUIFF()
        {
#if GPUI_PRO_PRESENT
            //HACK to be removed
            //This if should go when we decide to keep GPUI enabled or not.
            //As of now, if we have to turn it off because of an emergency situation, we need to regenerate the cache.
            //GPUI cache and regular terrain cache are not compatible
            //Also, when decision is taken, make `forceCacheRegen` private again
            int storedGPUIValue = DCLPlayerPrefs.GetInt(DCLPrefKeys.GPUI_ENABLED);
            bool wasEnabled = storedGPUIValue == 1;

            if (isGPUIEnabledFF != wasEnabled)
                terrainGenerator.forceCacheRegen = true;

            DCLPlayerPrefs.SetInt(DCLPrefKeys.GPUI_ENABLED, isGPUIEnabledFF ? 1 : 0);

            if (isGPUIEnabledFF)
                gpuiWrapper = new GPUIWrapper();
            else
                gpuiWrapper = new MockGPUIWrapper();
#else
            gpuiWrapper = new MockGPUIWrapper();
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, floor);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, floor, realmPartitionSettings.Value, landscapeData.Value);
            LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeCollidersCullingSystem.InjectToWorld(ref builder, terrainGenerator, scenesCache, loadingStatus);

            gpuiWrapper.InjectDebugSystem(ref builder, debugContainerBuilder);
        }
    }
}
