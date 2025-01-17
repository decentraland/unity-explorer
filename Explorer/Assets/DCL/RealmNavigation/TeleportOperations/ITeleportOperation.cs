using CommunicationData.URLHelpers;
using DCL.AsyncLoadReporting;
using DCL.RealmNavigation.LoadingOperation;
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

        public TeleportParams(URLDomain currentDestinationRealm, Vector2Int currentDestinationParcel, AsyncLoadProcessReport report, ILoadingStatus loadingStatus)
        {
            CurrentDestinationRealm = currentDestinationRealm;
            CurrentDestinationParcel = currentDestinationParcel;
            Report = report;
            LoadingStatus = loadingStatus;
        }

        public void ChangeDestination(URLDomain newDestinationRealm, Vector2Int newDestinationParcel)
        {
            CurrentDestinationRealm = newDestinationRealm;
            CurrentDestinationParcel = newDestinationParcel;
        }
    }
}
