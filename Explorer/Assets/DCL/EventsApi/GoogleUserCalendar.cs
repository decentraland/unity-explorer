using CommunicationData.URLHelpers;
using DCL.Browser;
using System;

namespace DCL.EventsApi
{
    public class GoogleUserCalendar : IUserCalendar
    {
        private const string GOOGLE_CALENDAR_DOMAIN = "https://www.google.com/calendar/event";
        private const string ACTION_PARAM = "action";
        private const string ACTION_TEMPLATE = "TEMPLATE";
        private const string TITLE_PARAM = "text";
        private const string DATES_PARAM = "dates";
        private const string DESCRIPTION_PARAM = "details";

        private readonly IWebBrowser webBrowser;
        private readonly URLBuilder urlBuilder = new ();

        public GoogleUserCalendar(IWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public void Add(string title, string description, DateTime startAt, DateTime endAt)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(GOOGLE_CALENDAR_DOMAIN))
                      .AppendParameter(new URLParameter(ACTION_PARAM, ACTION_TEMPLATE))
                      .AppendParameter(new URLParameter(TITLE_PARAM, title))
                      .AppendParameter(new URLParameter(DATES_PARAM, $"{startAt:yyyyMMddTHHmmssZ}/{endAt:yyyyMMddTHHmmssZ}"))
                      .AppendParameter(new URLParameter(DESCRIPTION_PARAM, description));

            webBrowser.OpenUrl(urlBuilder.Build());
        }
    }
}
