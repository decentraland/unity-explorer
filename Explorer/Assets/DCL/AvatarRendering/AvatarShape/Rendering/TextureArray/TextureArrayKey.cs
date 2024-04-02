using System;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public readonly struct TextureArrayKey : IEquatable<TextureArrayKey>
    {
        public readonly int Id;
        public readonly int Resolution;

        public TextureArrayKey(int id, int resolution)
        {
            Id = id;
            Resolution = resolution;
        }

        public bool Equals(TextureArrayKey other) =>
            Id == other.Id && Resolution == other.Resolution;

        public override bool Equals(object obj) =>
            obj is TextureArrayKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Id, Resolution);
    }
}
