using DCL.PlacesAPIService;
using System;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Where the session lands: a parcel of Genesis City, or a world (the parcel is then only a stand-in for offline display).
    /// </summary>
    internal readonly struct LandingDestination : IEquatable<LandingDestination>
    {
        private const string GENESIS_PLAZA_TITLE = "Genesis Plaza";

        public readonly Vector2Int Parcel;
        public readonly string? WorldName;

        public LandingDestination(Vector2Int parcel, string? worldName)
        {
            Parcel = parcel;
            WorldName = worldName;
        }

        public bool Equals(LandingDestination other) =>
            Parcel == other.Parcel && WorldName == other.WorldName;

        public override bool Equals(object? obj) =>
            obj is LandingDestination other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Parcel, WorldName);

        public PlacesData.PlaceInfo ToOfflinePlace() =>
            new (Parcel)
            {
                title = WorldName ?? (Parcel == Vector2Int.zero ? GENESIS_PLAZA_TITLE : $"{Parcel.x},{Parcel.y}"),
                world_name = WorldName ?? string.Empty,
                base_position_processed = Parcel,
            };
    }
}
