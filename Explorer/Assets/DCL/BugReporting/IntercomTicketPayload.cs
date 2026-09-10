using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.BugReporting
{
    /// <summary>Codes of the "Platform" list attribute as the proxy takes them; it maps each code to Intercom's option id.</summary>
    public enum IntercomTicketPlatform
    {
        Desktop = 0,
        Mobile = 1,
    }

    /// <summary>One image the proxy uploads and inlines into the ticket description.</summary>
    public readonly struct EvidenceImage
    {
        public readonly byte[] Bytes;
        public readonly string ContentType;

        public EvidenceImage(byte[] bytes, string contentType)
        {
            Bytes = bytes;
            ContentType = contentType;
        }
    }

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
        public IntercomTicketPlatform Platform;
        public string? SdkVersion;
        public string? LauncherVersion;

        /// <summary>Option id of the "Meets Minimum Requirements" list attribute: Intercom takes the id, never the label.</summary>
        public string? MeetsMinimumRequirementsOptionId;

        /// <summary>Inlined into the description in this order; the proxy numbers them when there is more than one.</summary>
        public IReadOnlyList<EvidenceImage>? Evidence;
    }

    public static class IntercomTicketPayload
    {
        /// <summary>The proxy rejects a bigger image, and with it the whole ticket.</summary>
        public const int MAX_EVIDENCE_BYTES = 3 * 1024 * 1024;

        /// <summary>The proxy rejects a fourth image, and with it the whole ticket.</summary>
        public const int MAX_EVIDENCE_IMAGES = 3;

        /// <summary>
        ///     Bound for all the images together. The proxy caps the request body at 4.5 MB and base64 inflates
        ///     the bytes by a third, so the images share the budget that one image alone may fill.
        /// </summary>
        public const int MAX_EVIDENCE_TOTAL_BYTES = MAX_EVIDENCE_BYTES;

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
                ["Platform"] = (int)data.Platform,
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

            if (data.Evidence is { Count: > 0 })
            {
                var evidence = new List<Dictionary<string, object>>(data.Evidence.Count);

                for (var i = 0; i < data.Evidence.Count; i++)
                    evidence.Add(new Dictionary<string, object>
                    {
                        ["content_type"] = data.Evidence[i].ContentType,
                        ["data"] = Convert.ToBase64String(data.Evidence[i].Bytes),
                    });

                payload["evidence"] = evidence;
            }

            return JsonConvert.SerializeObject(payload);
        }
    }
}
