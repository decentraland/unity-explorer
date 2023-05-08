using CRDT;
using CrdtEcsBridge.Serialization;
using System;
using Utility;

namespace CrdtEcsBridge.Components.Transform
{
    public class SDKTransformSerializer : IComponentSerializer<SDKTransform>
    {
        public void DeserializeInto(SDKTransform instance, in ReadOnlySpan<byte> data)
        {
            var pointer = data;
            instance.Position.Set(pointer.Read<float>(), pointer.Read<float>(), pointer.Read<float>());
            instance.Rotation.Set(pointer.Read<float>(), pointer.Read<float>(), pointer.Read<float>(), pointer.Read<float>());
            instance.Scale.Set(pointer.Read<float>(), pointer.Read<float>(), pointer.Read<float>());
            instance.ParentId = pointer.Read<CRDTEntity>();
        }
    }
}
