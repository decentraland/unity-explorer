using DCL.AsyncLoadReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation.LoadingOperation;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public interface IStartupOperation : ILoadingOperation<IStartupOperation.Params>
    {
        public readonly struct Params : ILoadingOperationParams
        {
            public Params(AsyncLoadProcessReport report, UserInAppInitializationFlowParameters flowParameters)
            {
                Report = report;
                FlowParameters = flowParameters;
            }

            public AsyncLoadProcessReport Report { get; }

            public UserInAppInitializationFlowParameters FlowParameters { get; }
        }
    }
}
