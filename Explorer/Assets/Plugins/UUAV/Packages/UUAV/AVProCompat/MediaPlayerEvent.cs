namespace UUAV.Compat
{
    // No-op event surface kept only for AVPro-shape parity: it never has listeners.
    public sealed class MediaPlayerEvent
    {
        public bool HasListeners() => false;

        public void RemoveAllListeners() { }
    }
}
