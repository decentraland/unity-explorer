using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class PulseRemoveIntentions : IRemoveIntentions
    {
        private readonly MutexSync mutexSync = new ();
        private readonly HashSet<RemoveIntention> set = new ();

        public void Enqueue(string walletId)
        {
            using (mutexSync.GetScope())
                set.Add(new RemoveIntention(walletId, RoomSource.PULSE));
        }

        /// <summary>Drops a pending remove so a newer re-join supersedes a not-yet-applied leave.</summary>
        public void Cancel(string walletId)
        {
            using (mutexSync.GetScope())
                set.Remove(new RemoveIntention(walletId, RoomSource.PULSE));
        }

        public OwnedBunch<RemoveIntention> Bunch() =>
            new (mutexSync, set);
    }
}
