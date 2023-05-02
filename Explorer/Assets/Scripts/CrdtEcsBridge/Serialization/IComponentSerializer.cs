using System;

namespace CrdtEcsBridge.Serialization
{
    public interface IComponentSerializer
    {
        void DeserializeInto(object instance, in ReadOnlySpan<byte> data);
    }

    public interface IComponentSerializer<in T> : IComponentSerializer where T: class, new()
    {
        void IComponentSerializer.DeserializeInto(object instance, in ReadOnlySpan<byte> data) =>
            DeserializeInto((T)instance, in data);

        void DeserializeInto(T instance, in ReadOnlySpan<byte> data);
    }
}
