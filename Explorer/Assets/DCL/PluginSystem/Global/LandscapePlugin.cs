using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Landscape;
using DCL.Landscape.Components;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.MapRenderer.ComponentsFactory;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLWorldPlugin<LandscapeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MapRendererTextureContainer textureContainer;
        private ProvidedAsset<LandscapeData> landscapeData;
        private readonly LandscapeAssetPoolManager poolManager;
        private ProvidedAsset<TextAsset> emptyParcelsData;
        private ProvidedAsset<TextAsset> ownedParcelsData;
        private NativeArray<int2> emptyParcels;
        private NativeHashSet<int2> ownedParcels;
        private TerrainGenerator terrainGenerator;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner, MapRendererTextureContainer textureContainer)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.textureContainer = textureContainer;
            poolManager = new LandscapeAssetPoolManager();
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
            emptyParcelsData = await assetsProvisioner.ProvideMainAssetAsync(settings.emptyParcels, ct);
            ownedParcelsData = await assetsProvisioner.ProvideMainAssetAsync(settings.ownedParcels, ct);

            ParseParcels();

            /*foreach (LandscapeAsset landscapeAsset in landscapeData.Value.assets)
            foreach (Transform prefab in landscapeAsset.assets)
                poolManager.Add(prefab, landscapeAsset.poolPreWarmCount / landscapeAsset.assets.Length);*/

            poolManager.Add(landscapeData.Value.mapChunk, 8 * 8);

            terrainGenerator = new TerrainGenerator(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels);

            // prepare for the hiccup
            terrainGenerator.GenerateTerrain();
            terrainGenerator.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems) { }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            LandscapeParcelInitializerSystem.InjectToWorld(ref builder, landscapeData.Value, poolManager, textureContainer);
            LandscapeParcelUnloadSystem.InjectToWorld(ref builder, poolManager);

            // Create all empty parcels
            foreach (int2 emptyParcel in emptyParcels)
                builder.World.Create(new LandscapeParcel(ParcelMathHelper.GetPositionByParcelPosition(new Vector2Int(emptyParcel.x, emptyParcel.y)), 0), new LandscapeParcelInitialization());

            builder.World.Create(new SatelliteView(), new LandscapeParcelInitialization());
        }

        private void ParseParcels()
        {
            string[] ownedParcelsRaw = ownedParcelsData.Value.text.Split('\n');
            string[] emptyParcelsRaw = emptyParcelsData.Value.text.Split('\n');

            ownedParcels = new NativeHashSet<int2>(ownedParcelsRaw.Length, Allocator.Persistent);
            emptyParcels = new NativeArray<int2>(emptyParcelsRaw.Length, Allocator.Persistent);

            foreach (string ownedParcel in ownedParcelsRaw)
            {
                string[] coordinates = ownedParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    ownedParcels.Add(new int2(x, y));
                else
                    Debug.LogWarning("Invalid line: " + ownedParcel);
            }

            for (var i = 0; i < emptyParcelsRaw.Length; i++)
            {
                string emptyParcel = emptyParcelsRaw[i];
                string[] coordinates = emptyParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    emptyParcels[i] = new int2(x, y);
                else
                    Debug.LogWarning("Invalid line: " + emptyParcel);
            }

            bool TryParse(string[] coords, out int x, out int y)
            {
                x = 0;
                y = 0;
                return coords.Length == 2 && int.TryParse(coords[0], out x) && int.TryParse(coords[1], out y);
            }
        }

        public void Dispose()
        {
            emptyParcels.Dispose();
            ownedParcels.Dispose();
            terrainGenerator?.Dispose();
        }
    }
}
