using DCL.Landscape.Settings;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Flatten (x,y) parcel coordinates into 1-dimensional array
    /// </summary>
    public class ParcelEncoder
    {
        private readonly TerrainGenerationData terrainData;

        public int MinX => GenesisCityData.MIN_PARCEL.x - terrainData.borderPadding;
        public int MinY => GenesisCityData.MIN_PARCEL.y - terrainData.borderPadding;
        public int MaxX => GenesisCityData.MAX_PARCEL.x + terrainData.borderPadding;
        public int MaxY => GenesisCityData.MAX_PARCEL.y + terrainData.borderPadding;

        private int width => MaxX - MinX + 1;

        public ParcelEncoder(TerrainGenerationData terrainData)
        {
            this.terrainData = terrainData;
        }

        public int Encode(Vector2Int parcel) =>
            parcel.x - MinX + ((parcel.y - MinY) * width);

        public Vector2Int Decode(int index) =>
            new ((index % width) + MinX, (index / width) + MinY);
    }
}
