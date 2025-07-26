using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

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
            int textureSize = ceilpow2(cmax(terrainSize) + 2);

            occupancyMap = new Texture2D(textureSize, textureSize, TextureFormat.R8, false, true);
            NativeArray<byte> data = occupancyMap.GetRawTextureData<byte>();

            // A square of red pixels surrounded by a border of black pixels totalPadding pixels wide
            // surrounded by red pixels to fill out the power of two texture, but at least one. World
            // origin (parcel 0,0) corresponds to uv of 0.5 plus half a pixel. The outer border is there
            // so that terrain height blends to zero at its edges.
            {
                int i = 0;

                // First section: rows of red pixels from the top edge of the texture to minParcel.y.
                int endY = (textureSize / 2 + minParcel.y) * textureSize;

                while (i < endY)
                    data[i++] = 255;

                // Second section: totalPadding rows of: one or more red pixels (enough to pad the
                // texture out to a power of two), terrainSize.x black pixels, one or more red pixels
                // again for padding.
                endY = i + totalPadding * textureSize;

                while (i < endY)
                {
                    int endX = i + textureSize / 2 + minParcel.x;

                    while (i < endX)
                        data[i++] = 255;

                    endX = i + terrainSize.x;

                    while (i < endX)
                        data[i++] = 0;

                    endX = i + textureSize / 2 - maxParcel.x - 1;

                    while (i < endX)
                        data[i++] = 255;
                }

                // Third, innermost section: citySize.y rows of: one or more red pixels, totalPadding
                // black pixels, citySize.x red pixels, totalPadding black pixels, one or more red
                // pixels.
                endY = i + citySize.y * textureSize;

                while (i < endY)
                {
                    int endX = i + textureSize / 2 + minParcel.x;

                    while (i < endX)
                        data[i++] = 255;

                    endX = i + totalPadding;

                    while (i < endX)
                        data[i++] = 0;

                    endX = i + citySize.x;

                    while (i < endX)
                        data[i++] = 255;

                    endX = i + totalPadding;

                    while (i < endX)
                        data[i++] = 0;

                    endX = i + textureSize / 2 - maxParcel.x - 1;

                    while (i < endX)
                        data[i++] = 255;
                }

                // Fourth section, same as second section.
                endY = i + totalPadding * textureSize;

                while (i < endY)
                {
                    int endX = i + textureSize / 2 + minParcel.x;

                    while (i < endX)
                        data[i++] = 255;

                    endX = i + terrainSize.x;

                    while (i < endX)
                        data[i++] = 0;

                    endX = i + textureSize / 2 - maxParcel.x - 1;

                    while (i < endX)
                        data[i++] = 255;
                }

                // Fifth section, same as first section.
                endY = i + (textureSize / 2 - maxParcel.y - 1) * textureSize;

                while (i < endY)
                    data[i++] = 255;
            }

            for (int i = 0; i < empty.Length; i++)
            {
                int2 parcel = empty[i] + textureSize / 2;
                data[parcel.y * textureSize + parcel.x] = 0;
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
            terrainData.GenerateTreePositions();

            // TODO: Remove before merging to dev.
            /*terrainData.GroundMaterial.mainTexture = occupancyMap;
            terrainData.GroundMaterial.mainTextureOffset = (float2)(-(minParcel - 1) / (SizeInParcels + 2));
            terrainData.GroundMaterial.mainTextureScale = (float2)1f / ((SizeInParcels + 2) * parcelSize);*/
        }
    }
}
