using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.BugReporting
{
    public struct IntercomTicketData
    {
        public string Title;
        public string Description;

        /// <summary>Option id of the "Issue Type" list attribute: Intercom takes the id, never the label.</summary>
        public string IssueTypeOptionId;

        public string OperatingSystem;
        public string GraphicCard;
        public string Ram;
        public string ClientVersion;
        public string? SdkVersion;
        public string? LauncherVersion;

        /// <summary>Option id of the "Meets Minimum Requirements" list attribute: Intercom takes the id, never the label.</summary>
        public string? MeetsMinimumRequirementsOptionId;

        public byte[]? EvidenceImage;
        public string? EvidenceContentType;
    }

    public static class IntercomTicketPayload
    {
        /// <summary>The proxy rejects a bigger image, and with it the whole ticket.</summary>
        public const int MAX_EVIDENCE_BYTES = 3 * 1024 * 1024;

        private const string DEFAULT_EVIDENCE_CONTENT_TYPE = "image/jpeg";

        /// <summary>
        ///     Builds the body of POST /intercom/tickets. The proxy accepts only ticket_attributes and evidence
        ///     at the top level, and only the attribute names the Bug Report ticket type declares.
        /// </summary>
        public static string BuildCreateTicketJson(in IntercomTicketData data)
        {
            var attributes = new Dictionary<string, object>
            {
                ["_default_title_"] = data.Title,
                ["_default_description_"] = data.Description,
                ["Issue Type"] = data.IssueTypeOptionId,
                ["Operating System"] = data.OperatingSystem,
                ["Graphic Card"] = data.GraphicCard,
                ["RAM"] = data.Ram,
                ["Client version"] = data.ClientVersion,
            };

            // Intercom keeps an absent attribute empty, while an empty string would show as a filled-in blank.
            if (!string.IsNullOrEmpty(data.SdkVersion))
                attributes["SDK version"] = data.SdkVersion;

            if (!string.IsNullOrEmpty(data.LauncherVersion))
                attributes["Launcher Version"] = data.LauncherVersion;

            if (!string.IsNullOrEmpty(data.MeetsMinimumRequirementsOptionId))
                attributes["Meets Minimum Requirements"] = data.MeetsMinimumRequirementsOptionId;

            var payload = new Dictionary<string, object>
            {
                ["ticket_attributes"] = attributes,
            };

            if (data.EvidenceImage is { Length: > 0 })
                payload["evidence"] = new Dictionary<string, object>
                {
                    ["content_type"] = data.EvidenceContentType ?? DEFAULT_EVIDENCE_CONTENT_TYPE,
                    ["data"] = Convert.ToBase64String(data.EvidenceImage),
                };

            return JsonConvert.SerializeObject(payload);
        }
    }
}
