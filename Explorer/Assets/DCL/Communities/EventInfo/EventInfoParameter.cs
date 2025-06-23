using DCL.EventsApi;

namespace DCL.Communities.EventInfo
{
    public class EventInfoParameter
    {
        public readonly IEventDTO eventData;

        public EventInfoParameter(IEventDTO eventData)
        {
            this.eventData = eventData;
        }
    }
}
