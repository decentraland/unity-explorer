using LiveKit.Rooms.ActiveSpeakers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullActiveSpeakers : IActiveSpeakers
    {
#pragma warning disable CS0067 // NullActiveSpeakers is a no-op IActiveSpeakers null object; this interface event is intentionally never raised
        public static readonly NullActiveSpeakers INSTANCE = new ();

        public int Count => 0;

        public event Action? Updated;

        public IEnumerator<string> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
#pragma warning restore CS0067
    }
}
