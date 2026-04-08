namespace DCL.WebRequests
{
    public interface IStreamableLoadingProgressHandler
    {
        float Progress { get; }
        long ContentLength { get; }
        void SetProgress(float progress);
        void SetContentLength(long length);
    }
}
