using Google.Protobuf;
using System;

namespace CrdtEcsBridge.Serialization
{
    public class ProtobufSerializer<T> : IComponentSerializer<T> where T: class, IMessage<T>, new()
    {
        public void DeserializeInto(T instance, in ReadOnlySpan<byte> data)
        {
            instance.MergeFrom(data);
        }

        public void SerializeInto(T model, in Span<byte> span) =>
            model.WriteTo(span);
    }
}
