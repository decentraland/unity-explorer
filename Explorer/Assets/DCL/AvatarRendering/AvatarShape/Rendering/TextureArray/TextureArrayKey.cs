using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public readonly struct TextureArrayKey : IEquatable<TextureArrayKey>
    {
        public readonly int Id;
        public readonly Vector2Int Resolution;
        public readonly int OptionValue;

        public TextureArrayKey(int id, Vector2Int resolution, int optionValue = 0)
        {
            Id = id;
            Resolution = resolution;
            OptionValue = optionValue;
        }

        public TextureArrayKey(int id, int squareResolution, int optionValue = 0)
        {
            Id = id;
            Resolution = new Vector2Int(squareResolution, squareResolution);
            OptionValue = optionValue;
        }

        public bool Equals(TextureArrayKey other) =>
            Id == other.Id && Resolution == other.Resolution && OptionValue == other.OptionValue;

        public override bool Equals(object obj) =>
            obj is TextureArrayKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Id, Resolution, OptionValue);
    }
}
