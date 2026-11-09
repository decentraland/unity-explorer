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
                Platform = IntercomTicketPlatform.Desktop,
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
            Assert.AreEqual(8, attributes.Count);
            Assert.AreEqual(data.Title, attributes["_default_title_"]!.Value<string>());
            Assert.AreEqual(data.Description, attributes["_default_description_"]!.Value<string>());
            Assert.AreEqual(data.IssueTypeOptionId, attributes["Issue Type"]!.Value<string>());
            Assert.AreEqual(data.OperatingSystem, attributes["Operating System"]!.Value<string>());
            Assert.AreEqual(data.GraphicCard, attributes["Graphic Card"]!.Value<string>());
            Assert.AreEqual(data.Ram, attributes["RAM"]!.Value<string>());
            Assert.AreEqual(data.ClientVersion, attributes["Client version"]!.Value<string>());
            Assert.AreEqual(1, attributes["Platform"]!.Value<int>());
        }

        [TestCase(IntercomTicketPlatform.Desktop, 1)]
        [TestCase(IntercomTicketPlatform.Mobile, 2)]
        public void SendThePlatformAsItsNumericCode(IntercomTicketPlatform platform, int expectedCode)
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.Platform = platform;

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - the proxy resolves the code to the list option; a label or 0 would be rejected.
            JToken platformToken = payload["ticket_attributes"]!["Platform"]!;
            Assert.AreEqual(JTokenType.Integer, platformToken.Type);
            Assert.AreEqual(expectedCode, platformToken.Value<int>());
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
            Assert.AreEqual(11, attributes.Count);
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
        public void EncodeEveryImageAsAnEvidenceArrayInOrder()
        {
            // Arrange
            byte[] first = Encoding.UTF8.GetBytes("first-image");
            byte[] second = Encoding.UTF8.GetBytes("second-image");
            IntercomTicketData data = ValidData();
            data.Evidence = new[] { new EvidenceImage(first, "image/png"), new EvidenceImage(second, "image/jpeg") };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - the proxy numbers the images in the order sent.
            Assert.AreEqual(2, payload.Count);
            JArray evidence = (JArray)payload["evidence"]!;
            Assert.AreEqual(2, evidence.Count);
            Assert.AreEqual("image/png", evidence[0]["content_type"]!.Value<string>());
            Assert.AreEqual(first, Convert.FromBase64String(evidence[0]["data"]!.Value<string>()!));
            Assert.AreEqual("image/jpeg", evidence[1]["content_type"]!.Value<string>());
            Assert.AreEqual(second, Convert.FromBase64String(evidence[1]["data"]!.Value<string>()!));
        }

        [Test]
        public void EncodeASingleImageAsAnArrayOfOne()
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.Evidence = new[] { new EvidenceImage(new byte[] { 0xff, 0xd8, 0xff }, "image/jpeg") };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.AreEqual(JTokenType.Array, payload["evidence"]!.Type);
            Assert.AreEqual(1, ((JArray)payload["evidence"]!).Count);
        }

        [Test]
        public void OmitEvidenceWhenThereAreNoImages()
        {
            // Arrange
            IntercomTicketData data = ValidData();
            data.Evidence = Array.Empty<EvidenceImage>();

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.IsNull(payload["evidence"]);
        }
    }
}
