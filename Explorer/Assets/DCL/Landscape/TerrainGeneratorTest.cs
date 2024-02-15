using Cysharp.Threading.Tasks;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    [ExecuteInEditMode]
    public class TerrainGeneratorTest : MonoBehaviour
    {
        public uint worldSeed = 1;
        public bool digHoles;
        public bool centerTerrain;
        public bool hideTrees;
        public bool hideDetails;
        public TerrainGenerationData genData;
        public ParcelData parcelData;

        private NativeParallelHashSet<int2> ownedParcels;
        private NativeArray<int2> emptyParcels;

        private void Start()
        {
            GenerateAsync().Forget();
        }

        [ContextMenu("Generate")]
        public async UniTask GenerateAsync()
        {
            ownedParcels = parcelData.GetOwnedParcels();
            emptyParcels = parcelData.GetEmptyParcels();

            var gen = new TerrainGenerator(genData, ref emptyParcels, ref ownedParcels, true);
            await gen.GenerateTerrainAsync(worldSeed, digHoles, centerTerrain, hideTrees, hideDetails);

            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }

    }
}
