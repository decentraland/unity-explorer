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

        public ReadOnlyMemory<byte> Serialize(T model)
        {
            //Is it the same as doing model.ToByteArray()?
            /*Span<byte> buffer = new byte[model.CalculateSize()].AsSpan();
            model.WriteTo(buffer);
            return buffer;*/

            var buffer = new byte[model.CalculateSize()];
            var output = new CodedOutputStream(buffer);
            model.WriteTo(output);

            return buffer.AsMemory();
        }
    }
}
