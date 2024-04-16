using Cysharp.Threading.Tasks;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainGeneratorTest : MonoBehaviour
    {
        public bool clearCache;
        public uint worldSeed = 1;
        public bool digHoles;
        public bool centerTerrain;
        public bool hideTrees;
        public bool hideDetails;
        public TerrainGenerationData genData;
        public ParcelData parcelData;

        private NativeParallelHashSet<int2> ownedParcels;
        private NativeArray<int2> emptyParcels;
        private TerrainGenerator gen;
        private WorldTerrainGenerator wGen;

        private void Start()
        {
            GenerateAsync().Forget();
        }

        // private void OnValidate()
        // {
        //     wGen = new WorldTerrainGenerator(genData);
        // }

        public TerrainGenerator GetGenerator() =>
            gen;

        [ContextMenu("Generate")]
        public async UniTask GenerateAsync()
        {
            ownedParcels = parcelData.GetOwnedParcels();
            emptyParcels = parcelData.GetEmptyParcels();

            if (genData.terrainSize == 1)
            {
                wGen = new WorldTerrainGenerator(genData);
                await wGen.GenerateTerrainAsync(ownedParcels, worldSeed);
            }
            else
            {
                gen = new TerrainGenerator(genData, ref emptyParcels, ref ownedParcels, true, clearCache);
                await gen.GenerateTerrainAsync(worldSeed, digHoles, centerTerrain, hideTrees, hideDetails, true);
            }

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }
}
