// ReSharper disable InconsistentNaming

using System;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Success body of POST /intercom/tickets. The proxy passes Intercom's ticket object through
    ///     verbatim on 200 (e.g. {"type":"ticket","id":"215475306696784","ticket_type":{...}}), and only
    ///     the id is consumed here. Error responses carry a non-2xx status with an {"error": ...} body,
    ///     which surfaces through the web request exception instead of this type.
    /// </summary>
    // Server schema: https://developers.intercom.com/docs/references/rest-api/api.intercom.io/tickets/ticket
    [Serializable]
    public class IntercomTicketResponse
    {
        public string id = null!;
    }
}
