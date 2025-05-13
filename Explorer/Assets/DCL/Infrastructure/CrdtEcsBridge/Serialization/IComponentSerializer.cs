using System;

namespace CrdtEcsBridge.Serialization
{
    public interface IComponentSerializer
    {
        void DeserializeInto(object instance, in ReadOnlySpan<byte> data);

        void SerializeInto(object model, in Span<byte> span);
    }

    public interface IComponentSerializer<in T> : IComponentSerializer where T: class, new()
    {
        void IComponentSerializer.DeserializeInto(object instance, in ReadOnlySpan<byte> data) =>
            DeserializeInto((T)instance, in data);

        void IComponentSerializer.SerializeInto(object instance, in Span<byte> span) =>
            SerializeInto((T)instance, span);

        void DeserializeInto(T instance, in ReadOnlySpan<byte> data);

        void SerializeInto(T model, in Span<byte> span);
    }
}
