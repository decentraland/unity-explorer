using System;

namespace DCL.EventsApi
{
    public class EventsApiException : Exception
    {
        public EventsApiException(string message) : base(message) { }

        public EventsApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
