// ReSharper disable InconsistentNaming

using System;

namespace DCL.BugReporting
{
    /// <summary>Success body of POST /intercom/tickets: Intercom's ticket object passed through verbatim, of which only the id is consumed.</summary>
    // Server schema: https://developers.intercom.com/docs/references/rest-api/api.intercom.io/tickets/ticket
    [Serializable]
    public class IntercomTicketResponse
    {
        public string id = null!;
    }
}
