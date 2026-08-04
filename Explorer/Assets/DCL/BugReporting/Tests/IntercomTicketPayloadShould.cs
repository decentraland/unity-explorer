using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.BugReporting.Tests
{
    public class IntercomTicketPayloadShould
    {
        [Test]
        public void BuildOnlyDefaultPseudoAttributes()
        {
            // Arrange
            var data = new IntercomTicketData
            {
                TicketTypeId = 4557778,
                Title = "Bug Report: Chat",
                Description = "The chat input loses focus.\n\n---\nInternal diagnostics: https://example.com",
            };

            // Act
            JObject payload = JObject.Parse(IntercomTicketPayload.BuildCreateTicketJson(in data));

            // Assert - any other attribute name risks Intercom rejecting the whole call.
            Assert.AreEqual(4557778L, payload["ticket_type_id"]!.Value<long>());

            JObject attributes = (JObject)payload["ticket_attributes"]!;
            Assert.AreEqual(2, attributes.Count);
            Assert.AreEqual(data.Title, attributes["_default_title_"]!.Value<string>());
            Assert.AreEqual(data.Description, attributes["_default_description_"]!.Value<string>());
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
