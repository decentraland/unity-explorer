using System;
using System.Buffers;

namespace CrdtEcsBridge.Serialization
{
    public struct DeserializationBuffer : IDisposable
    {
        // For communication only reference types are allowed
        // to avoid boxing
        private object[] components;

        private int index;
        private readonly int size;

        public DeserializationBuffer(int size)
        {
            this.size = size;
            components = ArrayPool<object>.Shared.Rent(size);
            index = 0;
        }

        public Span<object> Components => components.AsSpan()[new Range(0, index)];

        public void Add<T>(T component) where T: class
        {
            if (index >= size)
                throw new ArgumentOutOfRangeException($"Deserialization buffer is improperly preallocated with size {components.Length}");

            components[index++] = component;
        }

        public void Dispose()
        {
            ArrayPool<object>.Shared.Return(components);
        }
    }
}
