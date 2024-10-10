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
        UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct);
    }

    public struct TeleportParams
    {
        public URLDomain CurrentDestinationRealm;
        public Vector2Int CurrentDestinationParcel;
        public AsyncLoadProcessReport ParentReport;
        public ILoadingStatus RealFlowLoadingStatus;
    }
}