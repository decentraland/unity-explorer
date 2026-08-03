using Newtonsoft.Json;
using System.Collections.Generic;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Content of one ticket-creation call. Attribute values ride under the exact names the
    ///     Intercom "Bug Report" ticket type declares: Intercom rejects the whole call when one
    ///     name is unknown to the type, and its error never names the offender.
    /// </summary>
    public struct IntercomTicketData
    {
        public long TicketTypeId;
        public string Title;
        public string Description;
        public string? IssueTypeOptionId;
        public string? OperatingSystem;
        public string? GraphicCard;
        public string? Ram;
        public string? ClientVersion;
    }

    public static class IntercomTicketPayload
    {
        /// <summary>
        ///     Builds the body of POST /intercom/tickets. The proxy accepts only ticket_type_id and
        ///     ticket_attributes at the top level: the reporter's wallet and contact are set server
        ///     side from the verified Signed Fetch signer.
        /// </summary>
        public static string BuildCreateTicketJson(in IntercomTicketData data)
        {
            var attributes = new Dictionary<string, object>
            {
                ["_default_title_"] = data.Title,
                ["_default_description_"] = data.Description,
            };

            AddIfNotEmpty(attributes, "Issue Type", data.IssueTypeOptionId);
            AddIfNotEmpty(attributes, "Operating System", data.OperatingSystem);
            AddIfNotEmpty(attributes, "Graphic Card", data.GraphicCard);
            AddIfNotEmpty(attributes, "RAM", data.Ram);
            AddIfNotEmpty(attributes, "Client version", data.ClientVersion);

            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["ticket_type_id"] = data.TicketTypeId,
                ["ticket_attributes"] = attributes,
            });
        }

        private static void AddIfNotEmpty(Dictionary<string, object> attributes, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                attributes[name] = value;
        }
    }
}
