using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using System;
using System.Threading;

namespace Global.Dynamic.TeleportOperations
{
    public class AnalyticsFlushTeleportOperation : TeleportOperationBase
    {
        private readonly IAnalyticsController analyticsController;

        public AnalyticsFlushTeleportOperation(IAnalyticsController analyticsController)
        {
            this.analyticsController = analyticsController;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            analyticsController.Flush();
            return UniTask.CompletedTask;
        }
    }
}
