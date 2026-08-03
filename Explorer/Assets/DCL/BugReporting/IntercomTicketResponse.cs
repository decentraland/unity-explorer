// ReSharper disable InconsistentNaming

using System;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Intercom's create-ticket response, passed through verbatim by the intercom-proxy lambda.
    /// </summary>
    // Server schema: https://developers.intercom.com/docs/references/rest-api/api.intercom.io/tickets/ticket
    [Serializable]
    public class IntercomTicketResponse
    {
        public string id = null!;
    }
}
