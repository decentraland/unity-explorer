namespace DCL.SDKComponents.Tween.Playground
{
    public interface INtpTimeService
    {
        public bool IsSynced { get; }
        public ulong ServerTimeMs { get; }
    }
}
