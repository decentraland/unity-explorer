using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Text;

namespace DCL.McpServer.Tests
{
    public class McpToolResultShould
    {
        [Test]
        public void WrapTextInASingleTextContentItemWithoutAnErrorFlag()
        {
            JObject payload = McpToolResult.Text("hello").Payload;

            var content = (JArray)payload["content"]!;
            Assert.That(content.Count, Is.EqualTo(1));
            Assert.That(content[0]["type"]!.Value<string>(), Is.EqualTo("text"));
            Assert.That(content[0]["text"]!.Value<string>(), Is.EqualTo("hello"));
            Assert.That(payload.ContainsKey("isError"), Is.False);
        }

        [Test]
        public void CarryBothAMirroringTextItemAndStructuredContent()
        {
            var structured = new JObject
            {
                ["count"] = 3,
                ["nested"] = new JObject { ["ok"] = true },
            };

            JObject payload = McpToolResult.TextWithStructured("mirror", structured).Payload;

            var content = (JArray)payload["content"]!;
            Assert.That(content.Count, Is.EqualTo(1));
            Assert.That(content[0]["type"]!.Value<string>(), Is.EqualTo("text"));
            Assert.That(content[0]["text"]!.Value<string>(), Is.EqualTo("mirror"));

            var structuredContent = (JObject)payload["structuredContent"]!;
            Assert.That(structuredContent["count"]!.Value<int>(), Is.EqualTo(3));
            Assert.That(structuredContent["nested"]!["ok"]!.Value<bool>(), Is.True);
            Assert.That(payload.ContainsKey("isError"), Is.False);
        }

        [Test]
        public void FlagErrorsWithIsErrorAndCarryTheMessageAsText()
        {
            JObject payload = McpToolResult.Error("it broke").Payload;

            Assert.That(payload["isError"]!.Value<bool>(), Is.True);

            var content = (JArray)payload["content"]!;
            Assert.That(content[0]["type"]!.Value<string>(), Is.EqualTo("text"));
            Assert.That(content[0]["text"]!.Value<string>(), Is.EqualTo("it broke"));
        }

        [Test]
        public void EncodeImageBytesAsBase64AlongsideACaption()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("pixels");

            JObject payload = McpToolResult.Image(bytes, "image/png", "a screenshot").Payload;

            var content = (JArray)payload["content"]!;
            Assert.That(content.Count, Is.EqualTo(2));

            var image = (JObject)content[0];
            Assert.That(image["type"]!.Value<string>(), Is.EqualTo("image"));
            Assert.That(image["mimeType"]!.Value<string>(), Is.EqualTo("image/png"));
            Assert.That(Convert.FromBase64String(image["data"]!.Value<string>()!), Is.EqualTo(bytes));

            var caption = (JObject)content[1];
            Assert.That(caption["type"]!.Value<string>(), Is.EqualTo("text"));
            Assert.That(caption["text"]!.Value<string>(), Is.EqualTo("a screenshot"));
            Assert.That(payload.ContainsKey("isError"), Is.False);
        }
    }
}
