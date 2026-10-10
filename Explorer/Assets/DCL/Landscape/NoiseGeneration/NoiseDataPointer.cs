using System;

namespace DCL.Landscape.NoiseGeneration
{
    public readonly struct NoiseDataPointer : IEquatable<NoiseDataPointer>
    {
        internal readonly int size;
        internal readonly int offsetX;
        internal readonly int offsetZ;

        public NoiseDataPointer(int size, int offsetX, int offsetZ)
        {
            this.size = size;
            this.offsetX = offsetX;
            this.offsetZ = offsetZ;
        }

        public bool Equals(NoiseDataPointer other) =>
            size == other.size && offsetX == other.offsetX && offsetZ == other.offsetZ;

        public override bool Equals(object obj) =>
            obj is NoiseDataPointer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(size, offsetX, offsetZ);
    }
}
