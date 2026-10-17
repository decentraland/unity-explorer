using DCL.Diagnostics;
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
            RoadCoordinate = roadCoordinate;
            Rotation = rotation;

            if (rotation is { x: 0, y: 0, z: 0, w: 0 })
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"Road {RoadModel} at {RoadCoordinate} has zero rotation! Using {Quaternion.identity}");
                Rotation = Quaternion.identity;
            }
        }
    }
}
