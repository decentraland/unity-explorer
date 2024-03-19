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

        public UniTaskVoid NotifyRemotesAsync()
        {
            if (DateTime.UtcNow - previousNotify < debounce)
                return new UniTaskVoid();

            previousNotify = DateTime.UtcNow;
            return origin.NotifyRemotesAsync();
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
