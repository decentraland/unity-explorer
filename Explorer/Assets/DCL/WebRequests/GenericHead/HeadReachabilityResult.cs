namespace DCL.WebRequests
{
    public readonly struct HeadReachabilityResult
    {
        public static readonly HeadReachabilityResult Unreachable = new (false, null);

        public readonly bool IsReachable;
        public readonly string ContentType;

        public HeadReachabilityResult(bool isReachable, string contentType)
        {
            IsReachable = isReachable;
            ContentType = contentType;
        }
    }
}
