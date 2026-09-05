using CommunicationData.URLHelpers;
using DCL.RealmNavigation.LoadingOperation;
using DCL.Utilities;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations
{
    public interface ITeleportOperation : ILoadingOperation<TeleportParams>
    {
    }

    public struct TeleportParams : ILoadingOperationParams
    {
        public URLDomain CurrentDestinationRealm { get; private set; }
        public Vector2Int CurrentDestinationParcel { get; private set; }
        public AsyncLoadProcessReport Report { get; }
        public ILoadingStatus LoadingStatus { get; }

        public bool AllowsWorldPositionOverride { get; private set; }

        /// <summary>
        ///     When true, land at <see cref="CurrentDestinationParcel" /> itself instead of the scene's spawn point.
        /// </summary>
        public bool LandOnParcel { get; private set; }

        /// <summary>
        ///     When set, land at the scene's spawn point with this name (case-insensitive); an unmatched name falls back to the default selection.
        /// </summary>
        public string? SpawnPointName { get; }

        public TeleportParams(URLDomain currentDestinationRealm, Vector2Int currentDestinationParcel, AsyncLoadProcessReport report, ILoadingStatus loadingStatus, bool allowsWorldPositionOverride, bool landOnParcel = false, string? spawnPointName = null)
        {
            CurrentDestinationRealm = currentDestinationRealm;
            CurrentDestinationParcel = currentDestinationParcel;
            Report = report;
            LoadingStatus = loadingStatus;
            AllowsWorldPositionOverride = allowsWorldPositionOverride;
            LandOnParcel = landOnParcel;
            SpawnPointName = spawnPointName;
        }

        public void ChangeDestination(URLDomain newDestinationRealm, Vector2Int newDestinationParcel)
        {
            CurrentDestinationRealm = newDestinationRealm;
            CurrentDestinationParcel = newDestinationParcel;
        }
    }
}
