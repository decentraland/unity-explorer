using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.EventsApi
{
    public interface IEventsApiService
    {
        UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelsAsync(ISet<string> parcels, CancellationToken ct);
    }
}
