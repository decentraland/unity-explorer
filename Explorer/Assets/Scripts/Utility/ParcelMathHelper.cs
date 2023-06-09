using System.Collections.Generic;
using UnityEngine;

namespace Utility
{
    public static class ParcelMathHelper
    {
        public const float PARCEL_SIZE = 16.0f;

        public static Vector3 GetPositionByParcelPosition(Vector2Int parcelPosition) =>
            new (parcelPosition.x * PARCEL_SIZE, 0.0f, parcelPosition.y * PARCEL_SIZE);

        public static IReadOnlyCollection<Vector2Int> ParcelsInRange(Vector3 position, int loadRadius, HashSet<Vector2Int> results)
        {
            float range = loadRadius * PARCEL_SIZE;
            Vector2 focus = new Vector2(position.x, position.z) * new Vector2(1.0f, 1.0f);

            Vector2 minPoint = focus - new Vector2(range, range);
            Vector2 maxPoint = focus + new Vector2(range, range);

            Vector2Int minParcel = Vector2Int.FloorToInt(minPoint / 16.0f);
            Vector2Int maxParcel = Vector2Int.CeilToInt(maxPoint / 16.0f);

            results.Clear();

            for (int parcelX = minParcel.x; parcelX < maxParcel.x; ++parcelX)
            {
                for (int parcelY = minParcel.y; parcelY < maxParcel.y; ++parcelY)
                {
                    Vector2 parcel = new Vector2(parcelX, parcelY);
                    Vector2 parcelMinPoint = parcel * PARCEL_SIZE;
                    Vector2 parcelMaxPoint = (parcel + new Vector2(1.0f, 1.0f)) * PARCEL_SIZE;

                    float nearestPointX = Mathf.Clamp(focus.x, parcelMinPoint.x, parcelMaxPoint.x);
                    float nearestPointY = Mathf.Clamp(focus.y, parcelMinPoint.y, parcelMaxPoint.y);
                    Vector2 nearestPoint = new Vector2(nearestPointX, nearestPointY);
                    float distance = Vector2.Distance(nearestPoint, focus);

                    if (distance < range) results.Add(new Vector2Int(parcelX, parcelY));
                }
            }

            return results;
        }
    }
}
