using REnum;
using UnityEngine;

namespace DCL.SceneRuntime.Apis.RestrictedActionsApi
{
    public readonly struct RealmDestination
    {
        public readonly string Realm;

        /// <summary>Absent targets the realm's default spawn.</summary>
        public readonly Vector2Int? Parcel;

        public RealmDestination(string realm, Vector2Int? parcel)
        {
            Realm = realm;
            Parcel = parcel;
        }
    }

    /// <summary>
    ///     Target of a teleport request: <c>Parcel</c> addresses a parcel in the realm the player is already in,
    ///     <c>Realm</c> switches realm and optionally lands on a parcel there.
    /// </summary>
    [REnum]
    [REnumField(typeof(Vector2Int), "Parcel")]
    [REnumField(typeof(RealmDestination), "Realm")]
    public readonly partial struct TeleportDestination
    {
        /// <summary>
        ///     False when the request carries neither a parcel nor a realm. An empty realm counts as absent.
        /// </summary>
        public static bool TryCreate(Vector2Int? parcel, string? realm, out TeleportDestination destination)
        {
            if (!string.IsNullOrEmpty(realm))
            {
                destination = FromRealm(new RealmDestination(realm, parcel));
                return true;
            }

            if (parcel.HasValue)
            {
                destination = FromParcel(parcel.Value);
                return true;
            }

            destination = default(TeleportDestination);
            return false;
        }
    }
}
