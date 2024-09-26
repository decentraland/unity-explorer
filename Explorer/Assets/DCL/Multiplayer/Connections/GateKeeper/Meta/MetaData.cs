using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    [Serializable]
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public struct MetaData : IEquatable<MetaData>
    {
        public string realmName;
        public string? sceneId;

        [NonSerialized]
        public readonly Vector2Int Parcel;

        public MetaData(string realmName, string? sceneId, Vector2Int parcel)
        {
            this.realmName = realmName;
            this.sceneId = sceneId;
            Parcel = parcel;
        }

        public string ToJson() =>
            JsonUtility.ToJson(this)!;

        public bool Equals(MetaData other) =>
            realmName == other.realmName && sceneId == other.sceneId;

        public override bool Equals(object? obj) =>
            obj is MetaData other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(realmName, sceneId);
    }
}
