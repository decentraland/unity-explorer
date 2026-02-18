//#if !UNITY_WEBGL

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
using GPUInstancerPro;
using System.Threading;
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
            LandscapeParcelController landscapeParcelController,
            bool enableLandscape,
            bool isZone,
            Landscape landscape)
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
            this.landscape = landscape;
            this.landscapeParcelData = landscapeParcelData;
            this.landscapeParcelController = landscapeParcelController;

            // gpuiWrapper = new GPUIWrapper();
        }

        public void Dispose()
        {
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:85"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (enableLandscape)
            {
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:88"); // SPECIAL_DEBUG_LINE_STATEMENT
                terrainGenerator.Dispose();
                worldTerrainGenerator.Dispose();
            }
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:96"); // SPECIAL_DEBUG_LINE_STATEMENT
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            floor = new SatelliteFloor(realmData, landscapeData.Value);

            await landscapeParcelController.InitializeAsync(settings.parsedParcels, ct);
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:102"); // SPECIAL_DEBUG_LINE_STATEMENT

            if (!enableLandscape) return;
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:105"); // SPECIAL_DEBUG_LINE_STATEMENT

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:107"); // SPECIAL_DEBUG_LINE_STATEMENT
            realmPartitionSettings = settings.realmPartitionSettings;

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:110"); // SPECIAL_DEBUG_LINE_STATEMENT
            var emptyParcelsRef = landscapeParcelData.GetEmptyParcelsList();
            var ownedParcelsRef = landscapeParcelData.OccupiedParcels;

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:114"); // SPECIAL_DEBUG_LINE_STATEMENT
            GPUIProfile treesProfile = landscapeData.Value.TreesProfile;
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:116"); // SPECIAL_DEBUG_LINE_STATEMENT
            LandscapeAsset[] treePrototypes = landscapeData.Value.terrainData.treeAssets;
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:118"); // SPECIAL_DEBUG_LINE_STATEMENT
            int[] treeRendererKeys = new int[treePrototypes.Length];

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:121"); // SPECIAL_DEBUG_LINE_STATEMENT
            for (int prototypeIndex = 0; prototypeIndex < treePrototypes.Length; prototypeIndex++)
            {
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:124"); // SPECIAL_DEBUG_LINE_STATEMENT
                UnityEngine.GameObject prefab = treePrototypes[prototypeIndex].asset;

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:127"); // SPECIAL_DEBUG_LINE_STATEMENT
                UnityEngine.Object root = landscape.Root;

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:130"); // SPECIAL_DEBUG_LINE_STATEMENT
                var result = await GPUICoreAPI.RegisterRenderer(root, prefab, treesProfile);
                treeRendererKeys[prototypeIndex] = result.rendererKey;

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:134"); // SPECIAL_DEBUG_LINE_STATEMENT
            }

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:137"); // SPECIAL_DEBUG_LINE_STATEMENT
            terrainGenerator.Initialize(landscapeData.Value.terrainData, treeRendererKeys,
                ref emptyParcelsRef, ref ownedParcelsRef, landscapeData.Value);

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:141"); // SPECIAL_DEBUG_LINE_STATEMENT
            await worldTerrainGenerator.InitializeAsync(landscapeData.Value.worldsTerrainData,
                treeRendererKeys, landscapeData.Value);
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:144"); // SPECIAL_DEBUG_LINE_STATEMENT
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:149"); // SPECIAL_DEBUG_LINE_STATEMENT
            LandscapeSatelliteSystem.InjectToWorld(ref builder, textureContainer, floor);

            if (!enableLandscape) return;

            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, floor,
                realmPartitionSettings, landscape, landscapeData.Value);

            //LandscapeTerrainCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);
            LandscapeMiscCullingSystem.InjectToWorld(ref builder, landscapeData.Value, terrainGenerator);

            //LandscapeCollidersCullingSystem.InjectToWorld(ref builder, terrainGenerator, scenesCache, loadingStatus);
            RenderGroundSystem.InjectToWorld(ref builder, landscape, landscapeData.Value);
            CollideTerrainSystem.InjectToWorld(ref builder, landscape, landscapeData.Value);

UnityEngine.Debug.Log("CALLED LandscapePlugin.cs:164"); // SPECIAL_DEBUG_LINE_STATEMENT
            // gpuiWrapper.InjectDebugSystem(ref builder, debugContainerBuilder);
        }
    }
}

//#endif
