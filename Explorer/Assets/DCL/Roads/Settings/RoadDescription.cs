using System;
using UnityEngine;

namespace DCL.Roads.Settings
{
    [Serializable]
    public struct RoadDescription : IEquatable<RoadDescription>
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

        public bool Equals(RoadDescription other) =>
            RoadCoordinate.Equals(other.RoadCoordinate) && Rotation.Equals(other.Rotation) && RoadModel == other.RoadModel;

        public override bool Equals(object obj) =>
            obj is RoadDescription other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RoadCoordinate, Rotation, RoadModel);
    }
}
