namespace DCL.UserInAppInitializationFlow
{
    public interface IReadOnlyRealFlowLoadingStatus
    {
        public RealFlowLoadingStatus.Stage CurrentStage { get; }
    }
}
