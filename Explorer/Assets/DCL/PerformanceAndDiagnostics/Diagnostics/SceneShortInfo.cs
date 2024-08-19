using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Brief information about a scene that can be used to display it as a reference
    /// </summary>
    public readonly struct SceneShortInfo
    {
        public readonly Vector2Int BaseParcel;
        public readonly string Name;

        public SceneShortInfo(Vector2Int baseParcel, string name)
        {
            BaseParcel = baseParcel;
            Name = name;
        }

        public bool Equals(SceneShortInfo other) =>
            BaseParcel.Equals(other.BaseParcel) && Name == other.Name;

        public override bool Equals(object? obj) =>
            obj is SceneShortInfo other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(BaseParcel, Name);

        public override string ToString() =>
            $"({BaseParcel.x},{BaseParcel.y}) - {Name}";
    }
}
