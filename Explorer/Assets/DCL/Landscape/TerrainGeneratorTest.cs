using Cysharp.Threading.Tasks;
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
        public TextAsset emptyParcelsData;
        public TextAsset ownedParcelsData;
        private NativeHashSet<int2> ownedParcels;
        private NativeArray<int2> emptyParcels;

        [ContextMenu("Generate")]
        public async UniTask Generate()
        {
            ParseParcels();
            var gen = new TerrainGenerator(genData, ref emptyParcels, ref ownedParcels);
            await gen.GenerateTerrain(worldSeed, digHoles, centerTerrain, hideTrees, hideDetails);
            gen.FreeMemory();
        }

        private void ParseParcels()
        {
            string[] ownedParcelsRaw = ownedParcelsData.text.Split('\n');
            string[] emptyParcelsRaw = emptyParcelsData.text.Split('\n');

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
