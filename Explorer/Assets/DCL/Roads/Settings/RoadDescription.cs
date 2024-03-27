using System;
using UnityEngine;

namespace DCL.Roads.Settings
{
    [Serializable]
    public struct RoadDescription
    {
        public Vector2Int RoadCoordinate;
        public Quaternion Rotation;
        public string RoadModel;

        public RoadDescription(Vector2Int roadCoordinate, string roadModel, Quaternion rotation)
        {
            RoadModel = roadModel;
            Rotation = rotation;
            RoadCoordinate = roadCoordinate;
        }
    }
}