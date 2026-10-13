namespace UUAV.Compat
{
    // No-op event surface kept only for AVPro-shape parity; the Explorer polls
    // Control and never touches Events.
    public sealed class MediaPlayerEvent
    {
        public bool HasListeners() => false;

        public void RemoveAllListeners() { }
    }
}
