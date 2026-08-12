using System.Collections.Generic;

namespace DCL.EventsApi
{
    /// <summary>
    ///     Total display order for event lists: live events first, then soonest next occurrence.
    ///     Ids break the remaining ties, so lists with equal sort keys always render in the same order
    ///     even under an unstable sort.
    /// </summary>
    public sealed class EventDisplayOrderComparer : IComparer<EventDTO>
    {
        public static readonly EventDisplayOrderComparer INSTANCE = new ();

        private EventDisplayOrderComparer() { }

        public int Compare(EventDTO x, EventDTO y)
        {
            if (x.live != y.live)
                return x.live ? -1 : 1;

            int byNextStart = x.NextStartAtProcessed.CompareTo(y.NextStartAtProcessed);
            return byNextStart != 0 ? byNextStart : string.CompareOrdinal(x.id, y.id);
        }
    }
}
