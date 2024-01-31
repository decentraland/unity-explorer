using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AsyncLoadReporting;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.Components;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Prioritization;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLGlobalPlugin<LandscapeSettings>
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private readonly MapRendererTextureContainer textureContainer;
        private ProvidedAsset<LandscapeData> landscapeData;
        private ProvidedAsset<TextAsset> emptyParcelsData;
        private ProvidedAsset<TextAsset> ownedParcelsData;
        private NativeArray<int2> emptyParcels;
        private NativeHashSet<int2> ownedParcels;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner, IDebugContainerBuilder debugContainerBuilder, MapRendererTextureContainer textureContainer)
        {
            terrainGenerator = new TerrainGenerator(landscapeData.Value.terrainData, ref emptyParcels, ref ownedParcels);
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
            this.textureContainer = textureContainer;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            emptyParcelsData = await assetsProvisioner.ProvideMainAssetAsync(settings.emptyParcels, ct);
            ownedParcelsData = await assetsProvisioner.ProvideMainAssetAsync(settings.ownedParcels, ct);

            ParseParcels();

            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, realmPartitionSettings.Value, landscapeData.Value);
            LandscapeParcelInitializerSystem.InjectToWorld(ref builder, landscapeData.Value, textureContainer);
            builder.World.Create(new SatelliteView(), new LandscapeParcelInitialization());
        }

        public void Dispose()
        {
            terrainGenerator.Dispose();
            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }

        public async UniTask InitializeLoadingProgress(AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            await terrainGenerator.GenerateTerrain(processReport: loadReport);

            // immediately dispose to free all memory used for generating the terrain
            terrainGenerator.FreeMemory();
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
    }
}
