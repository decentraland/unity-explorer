using DCL.EventsApi;

namespace DCL.Communities.EventInfo
{
    public class EventInfoParameter
    {
        public readonly IEventDTO EventData;

        public EventInfoParameter(IEventDTO eventData)
        {
            this.EventData = eventData;
        }
    }
}
