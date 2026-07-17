using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class McpInputSchemaShould
    {
        [Test]
        public void EmitTheDeclaredJsonSchemaTypeForEachFieldKind()
        {
            JObject schema = McpInputSchema.Object()
                                           .Number("ratio")
                                           .Boolean("flag")
                                           .Build();

            var properties = (JObject)schema["properties"]!;
            Assert.That(properties["ratio"]!["type"]!.Value<string>(), Is.EqualTo("number"));
            Assert.That(properties["flag"]!["type"]!.Value<string>(), Is.EqualTo("boolean"));
        }

        [Test]
        public void OmitDescriptionAndEnumWhenNotProvided()
        {
            JObject schema = McpInputSchema.Object().String("name").Build();

            var field = (JObject)schema["properties"]!["name"]!;
            Assert.That(field.ContainsKey("description"), Is.False);
            Assert.That(field.ContainsKey("enum"), Is.False);
        }

        [Test]
        public void CollectEveryRequiredFieldInDeclarationOrder()
        {
            JObject schema = McpInputSchema.Object()
                                           .String("first", required: true)
                                           .Integer("skipped")
                                           .Boolean("second", required: true)
                                           .Build();

            Assert.That(((JArray)schema["required"]!).ToObject<string[]>(), Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void ProduceAnEmptyPropertiesObjectForAnArgumentlessTool()
        {
            JObject schema = McpInputSchema.Object().Build();

            Assert.That(schema["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(((JObject)schema["properties"]!).Count, Is.EqualTo(0));
            Assert.That(schema.ContainsKey("required"), Is.False);
        }
    }
}
