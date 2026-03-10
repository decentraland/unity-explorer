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

        public TeleportParams(URLDomain currentDestinationRealm, Vector2Int currentDestinationParcel, AsyncLoadProcessReport report, ILoadingStatus loadingStatus, bool allowsWorldPositionOverride)
        {
            CurrentDestinationRealm = currentDestinationRealm;
            CurrentDestinationParcel = currentDestinationParcel;
            Report = report;
            LoadingStatus = loadingStatus;
            AllowsWorldPositionOverride = allowsWorldPositionOverride;
        }

        public void ChangeDestination(URLDomain newDestinationRealm, Vector2Int newDestinationParcel)
        {
            CurrentDestinationRealm = newDestinationRealm;
            CurrentDestinationParcel = newDestinationParcel;
        }
    }
}
