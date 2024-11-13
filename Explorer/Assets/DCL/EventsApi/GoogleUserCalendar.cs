using CommunicationData.URLHelpers;
using DCL.Browser;
using System;

namespace DCL.EventsApi
{
    public class GoogleUserCalendar : IUserCalendar
    {
        private readonly IWebBrowser webBrowser;
        private readonly URLBuilder urlBuilder = new ();

        public GoogleUserCalendar(IWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public void Add(string title, string description, DateTime startAt, DateTime endAt)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString("https://www.google.com/calendar/event"))
                      .AppendParameter(new URLParameter("action", "TEMPLATE"))
                      .AppendParameter(new URLParameter("text", title))
                      .AppendParameter(new URLParameter("dates", $"{startAt:yyyyMMddTHHmmssZ}/{endAt:yyyyMMddTHHmmssZ}"))
                      .AppendParameter(new URLParameter("details", description));

            webBrowser.OpenUrl(urlBuilder.Build());
        }
    }
}
