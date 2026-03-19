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

        public void Enqueue(string walletId) =>
            set.Add(new RemoveIntention(walletId, RoomSource.PULSE));

        public OwnedBunch<RemoveIntention> Bunch() =>
            // TODO: no need mutex really
            new (mutexSync, set);
    }
}
