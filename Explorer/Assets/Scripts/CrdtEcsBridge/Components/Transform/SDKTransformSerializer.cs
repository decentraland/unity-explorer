using CRDT;
using CrdtEcsBridge.Serialization;
using System;
using UnityEngine;
using Utility;

namespace CrdtEcsBridge.Components.Transform
{
    public class SDKTransformSerializer : IComponentSerializer<SDKTransform>
    {
        public void DeserializeInto(SDKTransform instance, in ReadOnlySpan<byte> data)
        {
            var pointer = data;
            instance.Position = pointer.Read<Vector3>();
            instance.Rotation = pointer.Read<Quaternion>();
            instance.Scale = pointer.Read<Vector3>();
            instance.ParentId = pointer.Read<CRDTEntity>();
        }
    }
}
