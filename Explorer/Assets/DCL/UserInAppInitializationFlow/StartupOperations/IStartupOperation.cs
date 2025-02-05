using DCL.RealmNavigation.LoadingOperation;
using DCL.Utilities;

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
