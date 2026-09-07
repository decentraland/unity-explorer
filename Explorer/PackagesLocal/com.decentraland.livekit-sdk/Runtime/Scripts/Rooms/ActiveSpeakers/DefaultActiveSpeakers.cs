using System;
using System.Collections;
using System.Collections.Generic;

namespace LiveKit.Rooms.ActiveSpeakers
{
    /// <summary>
    ///     Thread-safe implementation. State might be partially filled. It's expected due Livekit nature. Snapshots are not guaranteed.
    ///     WebGL-safe implementation. Single-thread assumption.
    /// </summary>
    public class DefaultActiveSpeakers : IMutableActiveSpeakers
    {
#if UNITY_WEBGL
        private readonly HashSet<string> actives = new();
#else
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> actives = new(); // Standard library does not have ConcurrentHashSet
#endif

        public int Count => actives.Count;

        public event Action? Updated;

        public void UpdateCurrentActives(IEnumerable<string> sids)
        {
            actives.Clear();

#if UNITY_WEBGL
            actives.UnionWith(sids);
#else
            foreach (string s in sids)
            {
                actives.TryAdd(s, 0);
            }
#endif

            Updated?.Invoke();
        }

        public void Clear()
        {
            actives.Clear();
            Updated?.Invoke();
        }

        public IEnumerator<string> GetEnumerator()
        {
#if UNITY_WEBGL
            return actives.GetEnumerator();
#else
            return actives.Keys.GetEnumerator();
#endif
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
