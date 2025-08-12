using UnityEngine;
using System;

namespace DCL.Utilities
{
    public readonly struct PlayerParcelData : IEquatable<PlayerParcelData>
    {
        public readonly Vector2Int ParcelPosition;
        public readonly string? SceneHash;
        public readonly bool IsEmptyScene;
        public readonly bool HasScene;

        public PlayerParcelData(Vector2Int parcelPosition, string? sceneHash = null, bool isEmptyScene = false, bool hasScene = false)
        {
            ParcelPosition = parcelPosition;
            SceneHash = sceneHash;
            IsEmptyScene = isEmptyScene;
            HasScene = hasScene;
        }

        public bool Equals(PlayerParcelData other) =>
            ParcelPosition.Equals(other.ParcelPosition) && SceneHash == other.SceneHash && IsEmptyScene == other.IsEmptyScene && HasScene == other.HasScene;

        public override bool Equals(object? obj) =>
            obj is PlayerParcelData other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ParcelPosition, SceneHash, IsEmptyScene, HasScene);
    }
}
