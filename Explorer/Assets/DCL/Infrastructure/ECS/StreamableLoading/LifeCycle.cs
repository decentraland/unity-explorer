namespace ECS.StreamableLoading
{
    public enum LifeCycle : byte
    {
        LoadingNotStarted = 0,
        LoadingInProgress = 1,
        LoadingFinished = 2,
        Applied = 3,
    }
}
