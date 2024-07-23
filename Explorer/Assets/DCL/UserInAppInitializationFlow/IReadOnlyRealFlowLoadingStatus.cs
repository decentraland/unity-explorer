using DCL.Utilities;

namespace DCL.UserInAppInitializationFlow
{
    public interface IReadOnlyRealFlowLoadingStatus
    {
        public ReactiveProperty<RealFlowLoadingStatus.Stage> CurrentStage { get; }
    }
}
