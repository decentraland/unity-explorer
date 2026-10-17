using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms.ActiveSpeakers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorActiveSpeakers : IActiveSpeakers, IInterior<IActiveSpeakers>
    {
        private IActiveSpeakers assigned = NullActiveSpeakers.INSTANCE;

        public int Count => assigned.EnsureAssigned().Count;

        public event Action? Updated;

        public void Assign(IActiveSpeakers value, out IActiveSpeakers? previous)
        {
            previous = assigned;
            previous.Updated -= OnUpdated;
            previous = null;

            assigned = value;

            value.Updated += OnUpdated;

            previous = previous is NullActiveSpeakers ? null : previous;
        }

        private void OnUpdated()
        {
            Updated?.Invoke();
        }

        public IEnumerator<string> GetEnumerator() =>
            assigned.EnsureAssigned().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
