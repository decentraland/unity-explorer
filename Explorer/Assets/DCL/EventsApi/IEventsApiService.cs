using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.EventsApi
{
    public interface IEventsApiService
    {
        /// <param name="parcels">Parcel in format: "x,y"</param>
        UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(ISet<string> parcels, CancellationToken ct, bool onlyLiveEvents = false);

        /// <param name="parcel">Parcel in format: "x,y"</param>
        UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(string parcel, CancellationToken ct, bool onlyLiveEvents = false);

        UniTask MarkAsInterestedAsync(string eventId, CancellationToken ct);

        UniTask MarkAsNotInterestedAsync(string eventId, CancellationToken ct);
    }
}
