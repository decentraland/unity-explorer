using LiveKit.Rooms.ActiveSpeakers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorActiveSpeakers : IActiveSpeakers, IInterior<IActiveSpeakers>
    {
        private IActiveSpeakers? assigned;

        public int Count => assigned.EnsureAssigned().Count;

        public event Action? Updated;

        public void Assign(IActiveSpeakers value, out IActiveSpeakers? previous)
        {
            previous = assigned;

            if (previous != null)
                previous.Updated -= OnUpdated;

            assigned = value;

            value.Updated += OnUpdated;
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
