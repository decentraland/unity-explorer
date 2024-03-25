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

        private void Start()
        {
            GenerateAsync().Forget();
        }

        public TerrainGenerator GetGenerator() =>
            gen;

        [ContextMenu("Generate")]
        public async UniTask GenerateAsync()
        {
            ownedParcels = parcelData.GetOwnedParcels();
            emptyParcels = parcelData.GetEmptyParcels();

            gen = new TerrainGenerator(genData, ref emptyParcels, ref ownedParcels, true, clearCache);
            await gen.GenerateTerrainAsync(worldSeed, digHoles, centerTerrain, hideTrees, hideDetails, true);

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }

    }
}
