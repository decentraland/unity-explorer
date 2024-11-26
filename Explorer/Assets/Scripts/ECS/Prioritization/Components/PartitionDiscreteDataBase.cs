using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Prioritization.Components
{
    public abstract class PartitionDiscreteDataBase
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 Forward { get; set; }

        public Vector2Int Parcel { get; set; }

        public bool IsDirty { get; set; }

        public void Clear()
        {
            Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); // MaxValue to avoid Nullable
            Rotation = Quaternion.identity;
            Forward = Vector3.zero;
            Parcel = Vector2Int.zero;
            IsDirty = false;
        }
    }
}
