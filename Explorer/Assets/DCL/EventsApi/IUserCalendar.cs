using System;

namespace DCL.EventsApi
{
    public interface IUserCalendar
    {
        void Add(string title, string description, DateTime startAt, DateTime endAt);
    }
}
