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
            instance.Position.Value = pointer.Read<Vector3>();
            instance.Rotation.Value = pointer.Read<Quaternion>();
            instance.Scale = pointer.Read<Vector3>();
            instance.ParentId = pointer.Read<CRDTEntity>();
        }

        public void SerializeInto(SDKTransform instance, in Span<byte> span)
        {
            Span<byte> pointer = span;
            pointer.Write(instance.Position.Value);
            pointer.Write(instance.Rotation.Value);
            pointer.Write(instance.Scale);
            pointer.Write(instance.ParentId);
        }
    }
}
