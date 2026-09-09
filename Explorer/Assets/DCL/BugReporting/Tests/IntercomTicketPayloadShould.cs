using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Text;

namespace DCL.BugReporting.Tests
{
    public class IntercomTicketPayloadShould
    {
        private static IntercomTicketData ValidData() =>
            new ()
            {
                Title = "Bug Report: Chat",
                Description = "The chat input loses focus.\n\n---\nInternal diagnostics: https://example.com",
                IssueTypeOptionId = "b2db7b2e-3634-4c9d-9f55-b732bfe41319",
                OperatingSystem = "Windows 11",
                GraphicCard = "Example GPU",
                Ram = "32768 MB",
                ClientVersion = "0.1.0",
            };

        [Test]
        public void BuildOnlyDeclaredAttributes()
        {
            // Arrange
            IntercomTicketData data = ValidData();

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - any attribute name the Bug Report type does not declare gets the whole call rejected.
            JObject attributes = (JObject)payload["ticket_attributes"]!;
            Assert.AreEqual(7, attributes.Count);
            Assert.AreEqual(data.Title, attributes["_default_title_"]!.Value<string>());
            Assert.AreEqual(data.Description, attributes["_default_description_"]!.Value<string>());
            Assert.AreEqual(data.IssueTypeOptionId, attributes["Issue Type"]!.Value<string>());
            Assert.AreEqual(data.OperatingSystem, attributes["Operating System"]!.Value<string>());
            Assert.AreEqual(data.GraphicCard, attributes["Graphic Card"]!.Value<string>());
            Assert.AreEqual(data.Ram, attributes["RAM"]!.Value<string>());
            Assert.AreEqual(data.ClientVersion, attributes["Client version"]!.Value<string>());
        }

        [Test]
        public void IncludeOptionalContextAttributesWhenKnown()
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.SdkVersion = "7.5.6";
            data.LauncherVersion = "1.4.2";
            data.MeetsMinimumRequirementsOptionId = BugReportMinimumSpecOptions.MEETS_MIN_SPEC;

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            JObject attributes = (JObject)payload["ticket_attributes"]!;
            Assert.AreEqual(10, attributes.Count);
            Assert.AreEqual("7.5.6", attributes["SDK version"]!.Value<string>());
            Assert.AreEqual("1.4.2", attributes["Launcher Version"]!.Value<string>());
            Assert.AreEqual(BugReportMinimumSpecOptions.MEETS_MIN_SPEC, attributes["Meets Minimum Requirements"]!.Value<string>());
        }

        [Test]
        public void SendOnlyAllowedTopLevelFields()
        {
            // Arrange
            IntercomTicketData data = ValidData();

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - the proxy rejects any top-level field beyond this one, ticket_type_id included.
            Assert.AreEqual(1, payload.Count);
            Assert.IsNotNull(payload["ticket_attributes"]);
        }

        [Test]
        public void EncodeEvidenceWhenAnImageIsAttached()
        {
            // Arrange
            byte[] image = Encoding.UTF8.GetBytes("image-bytes");
            IntercomTicketData data = ValidData();
            data.EvidenceImage = image;
            data.EvidenceContentType = "image/png";

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.AreEqual(2, payload.Count);
            JObject evidence = (JObject)payload["evidence"]!;
            Assert.AreEqual("image/png", evidence["content_type"]!.Value<string>());
            Assert.AreEqual(image, Convert.FromBase64String(evidence["data"]!.Value<string>()!));
        }

        [Test]
        public void DefaultTheEvidenceContentTypeToJpeg()
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.EvidenceImage = new byte[] { 0xff, 0xd8, 0xff };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.AreEqual("image/jpeg", payload["evidence"]!["content_type"]!.Value<string>());
        }

        [Test]
        public void OmitEvidenceWhenTheImageIsMissingOrEmpty()
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.EvidenceImage = Array.Empty<byte>();

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.IsNull(payload["evidence"]);
        }
    }
}
