using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.MapRenderer.ComponentsFactory;
using ECS;
using ECS.Prioritization;
using GPUInstancerPro;
using System.Threading;
using DCL.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using LandscapeDebugSystem = DCL.Landscape.Systems.LandscapeDebugSystem;

namespace DCL.PluginSystem.Global
{
    using Landscape = global::Global.Dynamic.Landscapes.Landscape;

    public class LandscapePlugin : IDCLGlobalPlugin<LandscapeSettings>
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly WorldTerrainGenerator worldTerrainGenerator;
        private readonly Landscape landscape;
        private readonly IRealmData realmData;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly bool enableLandscape;

        private RealmPartitionSettingsAsset realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeData;
        private NativeParallelHashSet<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;
        private SatelliteFloor? floor;

        // private IGPUIWrapper gpuiWrapper;

        public LandscapePlugin(IRealmData realmData,
            TerrainGenerator terrainGenerator,
            WorldTerrainGenerator worldTerrainGenerator,
            IAssetsProvisioner assetsProvisioner,
            IDebugContainerBuilder debugContainerBuilder,
            MapRendererTextureContainer textureContainer,
            bool enableLandscape,
            Landscape landscape)
        {
            this.realmData = realmData;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
            this.enableLandscape = enableLandscape;
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;
            this.landscape = landscape;

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

            GPUIProfile treesProfile = landscapeData.Value.TreesProfile;
            LandscapeAsset[] treePrototypes = landscapeData.Value.terrainData.treeAssets;
            int[] treeRendererKeys = new int[treePrototypes.Length];

            for (int prototypeIndex = 0; prototypeIndex < treePrototypes.Length; prototypeIndex++)
            {
                GPUICoreAPI.RegisterRenderer(landscape.Root, treePrototypes[prototypeIndex].asset,
                    treesProfile, out treeRendererKeys[prototypeIndex]);

                ReportHub.Log(ReportCategory.LANDSCAPE, $"LandscapePlugin: Registered Renderer Key {treeRendererKeys[prototypeIndex]} for prototype {prototypeIndex} ({treePrototypes[prototypeIndex].asset.name})");
            }

            terrainGenerator.Initialize(landscapeData.Value.terrainData, treeRendererKeys, landscapeData.Value);

            await worldTerrainGenerator.InitializeAsync(landscapeData.Value.worldsTerrainData,
                treeRendererKeys, landscapeData.Value);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, floor);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, floor,
                realmPartitionSettings, landscape, landscapeData.Value);

            //LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);

            //LandscapeCollidersCullingSystem.InjectToWorld(ref builder, terrainGenerator, scenesCache, loadingStatus);
            RenderGroundSystem.InjectToWorld(ref builder, landscape, landscapeData.Value);
            CollideTerrainSystem.InjectToWorld(ref builder, landscape, landscapeData.Value);

            // gpuiWrapper.InjectDebugSystem(ref builder, debugContainerBuilder);
        }
    }
}
