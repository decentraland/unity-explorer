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
            ReadOnlySpan<byte> pointer = data;
            instance.Position = pointer.Read<Vector3>();
            instance.Rotation = pointer.Read<Quaternion>();
            instance.Scale = pointer.Read<Vector3>();
            instance.ParentId = pointer.Read<CRDTEntity>();
        }

        public void SerializeInto(SDKTransform instance, in Span<byte> span)
        {
            Span<byte> pointer = span;
            pointer.Write(instance.Position);
            pointer.Write(instance.Rotation);
            pointer.Write(instance.Scale);
            pointer.Write(instance.ParentId);
        }
    }
}
