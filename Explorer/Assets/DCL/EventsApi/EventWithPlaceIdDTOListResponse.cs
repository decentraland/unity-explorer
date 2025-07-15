using System;

namespace DCL.EventsApi
{
    [Serializable]
    public struct EventWithPlaceIdDTOListResponse
    {
        [Serializable]
        public struct EventPaginatedResult
        {
            public EventWithPlaceIdDTO[] events;
            public int total;
        }

        public bool ok;
        public EventPaginatedResult data;
    }
}
