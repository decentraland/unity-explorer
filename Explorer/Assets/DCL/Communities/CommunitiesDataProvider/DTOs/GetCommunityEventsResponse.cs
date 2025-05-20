
using DCL.EventsApi;
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityEventsResponse
    {
        public EventDTO[] events;
        public int totalPages;
    }
}
