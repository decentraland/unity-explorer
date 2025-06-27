using DCL.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TerrainData = Decentraland.Terrain.TerrainData;

namespace DCL.Landscape
{
    public class TerrainModel
    {
        private readonly int parcelSize;
        private readonly int2 minParcel;
        private readonly int2 maxParcel;
        private readonly Texture2D occupancyMap;

        public int2 MinParcel => minParcel;
        public int2 MaxParcel => maxParcel;
        public int2 SizeInParcels => MaxParcel - MinParcel + 1;
        public int2 SizeInUnits => SizeInParcels * parcelSize;
        public int2 MinInUnits => MinParcel * parcelSize;
        public int2 MaxInUnits => MinInUnits + SizeInUnits;
        public Texture2D OccupancyMap => occupancyMap;

        public TerrainModel(int2[] roads, int2[] occupied, int2[] empty, int parcelSize, int padding,
            float extraPadding = 0f)
        {
            this.parcelSize = parcelSize;

            CalculateMinMaxParcels(roads, ref minParcel, ref maxParcel);
            CalculateMinMaxParcels(occupied, ref minParcel, ref maxParcel);
            CalculateMinMaxParcels(empty, ref minParcel, ref maxParcel);

            int2 size = SizeInParcels;
            int totalPadding = padding + (int)math.round((size.x + size.y) * extraPadding);
            /*minParcel -= totalPadding;
            maxParcel += totalPadding;*/
            size = SizeInParcels + 2;

            occupancyMap = new Texture2D(size.x, size.y, TextureFormat.R8, false, true);
            NativeArray<byte> data = occupancyMap.GetRawTextureData<byte>();

            for (int i = 0; i < data.Length; i++)
                data[i] = 255;

            for (int i = 0; i < empty.Length; i++)
            {
                int2 parcel = empty[i];
                data[(parcel.y - minParcel.y + 1) * size.x + parcel.x - minParcel.x + 1] = 0;
            }

            occupancyMap.Apply(false, false);
        }

        private static void CalculateMinMaxParcels(int2[] parcels, ref int2 minParcel,
            ref int2 maxParcel)
        {
            foreach (int2 parcel in parcels)
            {
                if (parcel.x < minParcel.x)
                    minParcel.x = parcel.x;

                if (parcel.x > maxParcel.x)
                    maxParcel.x = parcel.x;

                if (parcel.y < minParcel.y)
                    minParcel.y = parcel.y;

                if (parcel.y > maxParcel.y)
                    maxParcel.y = parcel.y;
            }
        }

        public bool IsInsideBounds(Vector2Int parcel) =>
            parcel.x >= MinParcel.x && parcel.x <= MaxParcel.x && parcel.y >= MinParcel.y && parcel.y <= MaxParcel.y;

        private static Vector2Int ToVector2Int(int2 value) =>
            new Vector2Int(value.x, value.y);

        public void UpdateTerrainData(TerrainData terrainData)
        {
            terrainData.Bounds = new RectInt(ToVector2Int(MinParcel), ToVector2Int(SizeInParcels));
            terrainData.OccupancyMap = occupancyMap;

            // TODO: Remove before merging to dev.
            /*terrainData.GroundMaterial.mainTexture = occupancyMap;
            terrainData.GroundMaterial.mainTextureOffset = (float2)(-(minParcel - 1) / (SizeInParcels + 2));
            terrainData.GroundMaterial.mainTextureScale = (float2)1f / ((SizeInParcels + 2) * parcelSize);*/
        }
    }
}
