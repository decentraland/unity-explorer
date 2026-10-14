namespace UUAV.Compat
{
    // No-op event surface: it never has listeners.
    public sealed class MediaPlayerEvent
    {
        public bool HasListeners() => false;

        public void RemoveAllListeners() { }
    }
}
