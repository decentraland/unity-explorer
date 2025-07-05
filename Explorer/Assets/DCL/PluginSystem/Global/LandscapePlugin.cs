using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Landscape;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.Landscape.Utils;
using DCL.MapRenderer.ComponentsFactory;
using DCL.RealmNavigation;
using DCL.WebRequests;
using Decentraland.Terrain;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using TerrainData = DCL.Landscape.TerrainData;

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
        private readonly LandscapeParcelService parcelService;

        private bool disposed;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeData;
        private SatelliteFloor? floor;

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
            bool isGPUIEnabledFF)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = enableLandscape;
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;

            parcelService = new LandscapeParcelService(webRequestController, isZone);

            PlayerPrefs.SetInt(FeatureFlagsStrings.GPUI_ENABLED, isGPUIEnabledFF ? 1 : 0);
        }

        public void Dispose()
        {
            if (enableLandscape)
                terrainGenerator.Dispose();

            disposed = true;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            // Do this first and await it last because it takes the longest because it talks to the
            // Internet.
            var fetchParcelTask = enableLandscape ? parcelService.LoadManifestAsync(ct) : default;

            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
            //landscapeData.Value.terrainData.detailDistance = landscapeData.Value.EnvironmentDistance;

            floor = new SatelliteFloor(realmData, landscapeData.Value);

            if (!enableLandscape) return;

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);

            worldTerrainGenerator.Initialize(landscapeData.Value.worldData,
                landscapeData.Value.terrainData);

            FetchParcelResult fetchParcelResult = await fetchParcelTask;

            int2[] roads;
            int2[] occupied;
            int2[] empty;

            if (!fetchParcelResult.Succeeded)
            {
                var parcelData = await assetsProvisioner.ProvideMainAssetAsync(settings.parsedParcels,
                    ct);

                roads = Array.Empty<int2>();
                occupied = parcelData.Value.ownedParcels;
                empty = parcelData.Value.emptyParcels;
            }
            else
            {
                roads = fetchParcelResult.Manifest.roads;
                occupied = fetchParcelResult.Manifest.occupied;
                empty = fetchParcelResult.Manifest.empty;
            }

            terrainGenerator.Initialize(landscapeData.Value.genesisCityData,
                landscapeData.Value.terrainData, roads, occupied, empty);

            TerrainLog.LogHandler = ReportHub.Instance;

            if (disposed)
                throw new ObjectDisposedException(nameof(LandscapePlugin));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, floor);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, floor,
                realmPartitionSettings.Value, landscapeData.Value,
                landscapeData.Value.terrainData);

            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            RenderTerrainSystem.InjectToWorld(ref builder, landscapeData.Value.terrainData);

            Transform terrainParent;
#if UNITY_EDITOR
            terrainParent = terrainGenerator.RootObject;
#else
            terrainParent = null;
#endif

            CollideTerrainSystem.InjectToWorld(ref builder, landscapeData.Value.terrainData,
                terrainParent);
        }
    }
}
