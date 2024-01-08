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
        private List<Vector2Int> emptyParcels;
        private List<Vector2Int> ownedParcels;

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

            foreach (LandscapeAsset landscapeAsset in landscapeData.Value.assets)
            foreach (Transform prefab in landscapeAsset.assets)
                poolManager.Add(prefab, landscapeAsset.poolPreWarmCount / landscapeAsset.assets.Length);

            poolManager.Add(landscapeData.Value.groundTile, 35000);
            poolManager.Add(landscapeData.Value.mapChunk, 8 * 8);
        }

        public void Dispose()
        {
            landscapeData.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems) { }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            LandscapeParcelInitializerSystem.InjectToWorld(ref builder, landscapeData.Value, poolManager, textureContainer);
            LandscapeParcelUnloadSystem.InjectToWorld(ref builder, poolManager);

            // Create all empty parcels
            foreach (Vector2Int emptyParcel in emptyParcels)
                builder.World.Create(new LandscapeParcel(ParcelMathHelper.GetPositionByParcelPosition(emptyParcel), 0), new LandscapeParcelInitialization());

            builder.World.Create(new SatelliteView(), new LandscapeParcelInitialization());
        }

        private void ParseParcels()
        {
            emptyParcels = new List<Vector2Int>();
            ownedParcels = new List<Vector2Int>();

            Parse(emptyParcelsData.Value, emptyParcels);
            Parse(ownedParcelsData.Value, ownedParcels);
        }

        private static void Parse(TextAsset textAsset, List<Vector2Int> list)
        {
            string[] lines = textAsset.text.Split('\n');

            foreach (string line in lines)
            {
                string[] coordinates = line.Trim().Split(',');

                if (coordinates.Length == 2 && int.TryParse(coordinates[0], out int x) && int.TryParse(coordinates[1], out int y)) { list.Add(new Vector2Int(x, y)); }
                else
                    Debug.LogWarning("Invalid line: " + line);
            }
        }
    }
}
