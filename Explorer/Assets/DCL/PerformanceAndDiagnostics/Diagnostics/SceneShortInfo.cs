using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Brief information about a scene that can be used to display it as a reference
    /// </summary>
    public readonly struct SceneShortInfo : IEquatable<SceneShortInfo>
    {
        public readonly Vector2Int BaseParcel;
        public readonly string Name;
        public readonly string? SdkVersion;

        public SceneShortInfo(Vector2Int baseParcel, string name, string? sdkVersion = null)
        {
            BaseParcel = baseParcel;
            Name = name;
            SdkVersion = sdkVersion;
        }

        public override string ToString() =>
            $"({BaseParcel.x},{BaseParcel.y}) - {Name}";

        public bool Equals(SceneShortInfo other) =>
            BaseParcel.Equals(other.BaseParcel) && Name == other.Name && SdkVersion == other.SdkVersion;

        public override bool Equals(object? obj) =>
            obj is SceneShortInfo other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(BaseParcel, Name, SdkVersion);
    }
}
