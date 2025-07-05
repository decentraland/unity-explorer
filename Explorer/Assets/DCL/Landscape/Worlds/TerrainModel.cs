using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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

        public TerrainModel(int2[] roads, int2[] occupied, int2[] empty, int parcelSize, int padding,
            float extraPadding = 0f)
        {
            this.parcelSize = parcelSize;

            CalculateMinMaxParcels(roads, ref minParcel, ref maxParcel);
            CalculateMinMaxParcels(occupied, ref minParcel, ref maxParcel);
            CalculateMinMaxParcels(empty, ref minParcel, ref maxParcel);

            int2 citySize = maxParcel - minParcel + 1;
            int totalPadding = padding + (int)math.round((citySize.x + citySize.y) * extraPadding);
            minParcel -= totalPadding;
            maxParcel += totalPadding;
            int2 terrainSize = citySize + totalPadding * 2;
            int2 textureSize = terrainSize + 2;

            occupancyMap = new Texture2D(textureSize.x, textureSize.y, TextureFormat.R8, false, true);
            NativeArray<byte> data = occupancyMap.GetRawTextureData<byte>();

            // A square of red pixels surrounded by a border of black pixels totalPadding pixels wide
            // surrounded by a border of red pixels one pixel wide. The outer border is there so that
            // terrain height blends to zero at its edges.
            try
            {
                int i = 0;

                // First section: a single row or red pixels.
                int endY = textureSize.x;

                while (i < endY)
                    data[i++] = 255;

                // Second section: totalPadding rows of: one red pixel, terrainSize.x black pixels, one
                // red pixel.
                endY = i + totalPadding * textureSize.x;

                while (i < endY)
                {
                    data[i++] = 255;
                    int endX = i + terrainSize.x;

                    while (i < endX)
                        data[i++] = 0;

                    data[i++] = 255;
                }

                // Third, innermost section: citySize.y rows of: one red pixel, totalPadding black
                // pixels, citySize.x red pixels, totalPadding black pixels, one red pixel.
                endY = i + citySize.y * textureSize.x;

                while (i < endY)
                {
                    data[i++] = 255;
                    int endX = i + totalPadding;

                    while (i < endX)
                        data[i++] = 0;

                    endX = i + citySize.x;

                    while (i < endX)
                        data[i++] = 255;

                    endX = i + totalPadding;

                    while (i < endX)
                        data[i++] = 0;

                    data[i++] = 255;
                }

                // Fourth section, same as second section.
                endY = i + totalPadding * textureSize.x;

                while (i < endY)
                {
                    data[i++] = 255;
                    int endX = i + terrainSize.x;

                    while (i < endX)
                        data[i++] = 0;

                    data[i++] = 255;
                }

                // Fifth section, same as first section.
                endY = i + textureSize.x;

                while (i < endY)
                    data[i++] = 255;
            }
            catch (IndexOutOfRangeException) { }

            for (int i = 0; i < empty.Length; i++)
            {
                int2 parcel = empty[i];
                data[(parcel.y - minParcel.y + 1) * textureSize.x + parcel.x - minParcel.x + 1] = 0;
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
