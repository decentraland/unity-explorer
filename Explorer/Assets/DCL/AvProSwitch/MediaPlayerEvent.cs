namespace DCL.AvProSwitch
{
    // The consumer never subscribes to events (it polls Control instead). It
    // only touches these two methods, and only for defensive cleanup, so they
    // are no-ops on both backends.
    public sealed class MediaPlayerEvent
    {
        public bool HasListeners() => false;

        public void RemoveAllListeners() { }
    }
}
