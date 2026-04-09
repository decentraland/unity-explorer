namespace DCL.SDKComponents.MediaStream
{
    public readonly struct ResolvedYouTubeUrl
    {
        public readonly string DirectUrl;
        public readonly bool IsLiveStream;
        public readonly float ExpiresAtRealtimeSinceStartup;

        public ResolvedYouTubeUrl(string directUrl, bool isLiveStream, float expiresAtRealtimeSinceStartup)
        {
            DirectUrl = directUrl;
            IsLiveStream = isLiveStream;
            ExpiresAtRealtimeSinceStartup = expiresAtRealtimeSinceStartup;
        }
    }
}
