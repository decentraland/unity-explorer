using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.BugReporting.Tests
{
    public class IntercomTicketPayloadShould
    {
        [Test]
        public void BuildRequiredAndContextAttributes()
        {
            // Arrange
            var data = new IntercomTicketData
            {
                TicketTypeId = 4557778,
                Title = "Bug Report: Chat",
                Description = "The chat input loses focus.\n\n---\nInternal diagnostics: https://example.com",
                IssueTypeOptionId = "b2db7b2e-3634-4c9d-9f55-b732bfe41319",
                OperatingSystem = "Windows 11",
                GraphicCard = "RTX 5070 Ti",
                Ram = "32768 MB",
                ClientVersion = "1.2.3",
            };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            Assert.AreEqual(4557778L, payload["ticket_type_id"]!.Value<long>());

            JObject attributes = (JObject)payload["ticket_attributes"]!;
            Assert.AreEqual(data.Title, attributes["_default_title_"]!.Value<string>());
            Assert.AreEqual(data.Description, attributes["_default_description_"]!.Value<string>());
            Assert.AreEqual(data.IssueTypeOptionId, attributes["Issue Type"]!.Value<string>());
            Assert.AreEqual(data.OperatingSystem, attributes["Operating System"]!.Value<string>());
            Assert.AreEqual(data.GraphicCard, attributes["Graphic Card"]!.Value<string>());
            Assert.AreEqual(data.Ram, attributes["RAM"]!.Value<string>());
            Assert.AreEqual(data.ClientVersion, attributes["Client version"]!.Value<string>());
        }

        [Test]
        public void OmitEmptyOptionalAttributes()
        {
            // Arrange
            var data = new IntercomTicketData
            {
                TicketTypeId = 4557778,
                Title = "Bug Report: Other",
                Description = "Something broke.",
            };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert
            JObject attributes = (JObject)payload["ticket_attributes"]!;
            Assert.AreEqual(2, attributes.Count);
            Assert.IsNull(attributes["Issue Type"]);
            Assert.IsNull(attributes["Operating System"]);
        }

        [Test]
        public void SendOnlyAllowedTopLevelFields()
        {
            // Arrange
            var data = new IntercomTicketData
            {
                TicketTypeId = 4557778,
                Title = "Bug Report: Scene",
                Description = "The door does not open.",
            };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - the proxy rejects any top-level field beyond these two, contacts included.
            Assert.AreEqual(2, payload.Count);
            Assert.IsNotNull(payload["ticket_type_id"]);
            Assert.IsNotNull(payload["ticket_attributes"]);
        }
    }
}
