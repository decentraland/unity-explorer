using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SentryDiagnosticStartupOperation : IStartupOperation
    {
        private readonly IRealmController realmController;
        private readonly DiagnosticsContainer diagnosticsContainer;

        public SentryDiagnosticStartupOperation(
            IRealmController realmController, DiagnosticsContainer diagnosticsContainer)
        {
            this.realmController = realmController;
            this.diagnosticsContainer = diagnosticsContainer;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            diagnosticsContainer.AddSentryScopeConfigurator((scope) =>
            {
                diagnosticsContainer.Sentry?.AddRealmInfoToScope(scope,
                    realmController.RealmData.Ipfs.CatalystBaseUrl.Value,
                    realmController.RealmData.Ipfs.ContentBaseUrl.Value,
                    realmController.RealmData.Ipfs.LambdasBaseUrl.Value);
            });
            return Result.SuccessResult();
        }
    }
}
