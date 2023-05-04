using System;

namespace CrdtEcsBridge.Serialization
{
    public interface IComponentSerializer
    {
        void DeserializeInto(object instance, in ReadOnlySpan<byte> data);

        ReadOnlyMemory<byte> Serialize(object instance);

    }

    public interface IComponentSerializer<in T> : IComponentSerializer where T: class, new()
    {
        void IComponentSerializer.DeserializeInto(object instance, in ReadOnlySpan<byte> data) =>
            DeserializeInto((T)instance, in data);

        ReadOnlyMemory<byte> IComponentSerializer.Serialize(object instance) =>
            Serialize((T)instance);

        void DeserializeInto(T instance, in ReadOnlySpan<byte> data);

        public ReadOnlyMemory<byte> Serialize(T model);
    }
}
