using Newtonsoft.Json;
using System.Collections.Generic;

namespace DCL.BugReporting
{
    /// <summary>Content of one ticket-creation call.</summary>
    public struct IntercomTicketData
    {
        public long TicketTypeId;
        public string Title;
        public string Description;
    }

    public static class IntercomTicketPayload
    {
        /// <summary>
        ///     Builds the body of POST /intercom/tickets. The proxy accepts only ticket_type_id and
        ///     ticket_attributes at the top level: the reporter's wallet and contact are set server
        ///     side from the verified Signed Fetch signer. Only the _default_title_ and
        ///     _default_description_ pseudo-attributes go out: Intercom rejects the whole call when
        ///     the ticket type does not declare a provided attribute name, and these two are the
        ///     only ones every type has, so all context rides inside the description body.
        /// </summary>
        public static string BuildCreateTicketJson(in IntercomTicketData data) =>
            JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["ticket_type_id"] = data.TicketTypeId,
                ["ticket_attributes"] = new Dictionary<string, object>
                {
                    ["_default_title_"] = data.Title,
                    ["_default_description_"] = data.Description,
                },
            });
    }
}
