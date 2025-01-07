using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow;
using UnityEngine;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public interface ITeleportOperation
    {
        UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct);
    }

    public struct TeleportParams
    {
        public URLDomain CurrentDestinationRealm { get; private set; }
        public Vector2Int CurrentDestinationParcel { get; private set; }
        public AsyncLoadProcessReport ParentReport { get; }
        public ILoadingStatus LoadingStatus { get; }

        public TeleportParams(URLDomain currentDestinationRealm, Vector2Int currentDestinationParcel, AsyncLoadProcessReport parentReport, ILoadingStatus loadingStatus)
        {
            CurrentDestinationRealm = currentDestinationRealm;
            CurrentDestinationParcel = currentDestinationParcel;
            ParentReport = parentReport;
            LoadingStatus = loadingStatus;
        }

        public void ChangeDestination(URLDomain newDestinationRealm, Vector2Int newDestinationParcel)
        {
            CurrentDestinationRealm = newDestinationRealm;
            CurrentDestinationParcel = newDestinationParcel;
        }
    }
}
