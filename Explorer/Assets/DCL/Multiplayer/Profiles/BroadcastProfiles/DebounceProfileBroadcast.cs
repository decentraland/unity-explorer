using Cysharp.Threading.Tasks;
using System;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class DebounceProfileBroadcast : IProfileBroadcast
    {
        private readonly IProfileBroadcast origin;
        private readonly TimeSpan debounce;
        private DateTime previousNotify;

        public DebounceProfileBroadcast(IProfileBroadcast origin) : this(origin, TimeSpan.FromSeconds(2)) { }

        public DebounceProfileBroadcast(IProfileBroadcast origin, TimeSpan debounce)
        {
            this.origin = origin;
            this.debounce = debounce;
        }

        public void NotifyRemotes()
        {
            if (DateTime.UtcNow - previousNotify < debounce)
                return;

            previousNotify = DateTime.UtcNow;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
