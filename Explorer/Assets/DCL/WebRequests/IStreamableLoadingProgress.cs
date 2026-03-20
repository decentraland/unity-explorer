namespace DCL.WebRequests
{
    public interface IStreamableLoadingProgress
    {
        float Progress { get; }
        long ContentLength { get; }
    }

    public interface IStreamableLoadingProgressHandler : IStreamableLoadingProgress
    {
        void SetProgress (float progress);
        void SetContentLength (long length);
    }
}
