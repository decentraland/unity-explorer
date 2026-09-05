using LiveKit.Rooms.ActiveSpeakers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullActiveSpeakers : IActiveSpeakers
    {
        public static readonly NullActiveSpeakers INSTANCE = new ();

        public int Count => 0;

        public event Action? Updated;

        public IEnumerator<string> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
