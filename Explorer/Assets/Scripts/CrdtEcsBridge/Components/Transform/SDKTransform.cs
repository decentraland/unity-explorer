using CRDT;
using DCL.ECSComponents;
using UnityEngine;

namespace CrdtEcsBridge.Components.Transform
{
    /// <summary>
    /// Special type (not Proto) to serialize/deserialize faster according to the ADR
    /// </summary>
    public class SDKTransform : IDirtyMarker
    {
        public Vector3 Position = Vector3.zero;
        public Vector3 Scale = Vector3.one;
        public Quaternion Rotation = Quaternion.identity;
        public CRDTEntity ParentId = 0;
        public bool IsDirty { get; set; }
    }
}
