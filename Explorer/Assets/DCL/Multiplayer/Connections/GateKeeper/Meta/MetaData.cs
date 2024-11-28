using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    [Serializable]
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public struct MetaData : IEquatable<MetaData>
    {
        public readonly struct Input : IEquatable<Input>
        {
            public readonly string RealmName;
            public readonly Vector2Int Parcel;

            public Input(string realmName, Vector2Int parcel)
            {
                RealmName = realmName;
                Parcel = parcel;
            }

            public bool Equals(Input other) =>
                RealmName == other.RealmName && Parcel.Equals(other.Parcel);

            public bool Equals(MetaData other) =>
                RealmName == other.realmName && Parcel.Equals(other.Parcel);

            public override bool Equals(object? obj) =>
                obj is Input other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(RealmName, Parcel);

            public override string ToString() =>
                $"Realm: {RealmName}, Parcel: {Parcel}";
        }

        public string realmName;
        public string? sceneId;

        [NonSerialized]
        public readonly Vector2Int Parcel;

        public MetaData(string? sceneId, Input input)
        {
            realmName = input.RealmName;
            Parcel = input.Parcel;
            this.sceneId = sceneId;
        }

        public string ToJson() =>
            JsonUtility.ToJson(this)!;

        public override string ToString() =>
            $"Realm: {realmName}, Scene: {sceneId}, Parcel: {Parcel}";

        public bool Equals(MetaData other) =>
            realmName == other.realmName && sceneId == other.sceneId;

        public override bool Equals(object? obj) =>
            obj is MetaData other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(realmName, sceneId);
    }
}
