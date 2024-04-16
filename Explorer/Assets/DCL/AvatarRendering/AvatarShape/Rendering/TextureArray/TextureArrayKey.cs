using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public readonly struct TextureArrayKey : IEquatable<TextureArrayKey>
    {
        public readonly int Id;
        public readonly Vector2Int Resolution;

        public TextureArrayKey(int id, Vector2Int resolution)
        {
            Id = id;
            Resolution = resolution;
        }

        public TextureArrayKey(int id, int squareResolution)
        {
            Id = id;
            Resolution = new Vector2Int(squareResolution, squareResolution);
        }

        public bool Equals(TextureArrayKey other) =>
            Id == other.Id && Resolution == other.Resolution;

        public override bool Equals(object obj) =>
            obj is TextureArrayKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Id, Resolution);
    }
}
