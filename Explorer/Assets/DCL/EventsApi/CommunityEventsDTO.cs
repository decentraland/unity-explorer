using System;

namespace DCL.EventsApi
{
    [Serializable]
    public struct CommunityEventsDTO
    {
        public int totalAmount;
        public EventDTO[] data;
    }
}
