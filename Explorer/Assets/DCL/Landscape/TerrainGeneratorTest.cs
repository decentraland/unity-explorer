using DCL.Landscape.Settings;
using System;
using Unity.Collections;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainGeneratorTest : MonoBehaviour
    {
        public bool digHoles;
        public bool centerTerrain;
        public TerrainGenerationData genData;
        public TextAsset emptyParcelsData;
        public TextAsset ownedParcelsData;
        private NativeHashSet<Vector2Int> ownedParcels;
        private NativeArray<Vector2Int> emptyParcels;

        [ContextMenu("Generate")]
        private void Generate()
        {
            ParseParcels();
            var gen = new TerrainGenerator(genData, ref emptyParcels, ref ownedParcels);
            gen.GenerateTerrain(digHoles, centerTerrain);
            ownedParcels.Dispose();
            emptyParcels.Dispose();
        }

        private void ParseParcels()
        {
            string[] ownedParcelsRaw = ownedParcelsData.text.Split('\n');
            string[] emptyParcelsRaw = emptyParcelsData.text.Split('\n');

            ownedParcels = new NativeHashSet<Vector2Int>(ownedParcelsRaw.Length, Allocator.Persistent);
            emptyParcels = new NativeArray<Vector2Int>(emptyParcelsRaw.Length, Allocator.Persistent);

            foreach (string ownedParcel in ownedParcelsRaw)
            {
                string[] coordinates = ownedParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    ownedParcels.Add(new Vector2Int(x, y));
                else
                    Debug.LogWarning("Invalid line: " + ownedParcel);
            }

            for (var i = 0; i < emptyParcelsRaw.Length; i++)
            {
                string emptyParcel = emptyParcelsRaw[i];
                string[] coordinates = emptyParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    emptyParcels[i] = new Vector2Int(x, y);
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
