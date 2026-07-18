using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class McpJsonSchemaShould
    {
        [Test]
        public void EmitTheDeclaredJsonSchemaTypeForEachFieldKind()
        {
            JObject schema = McpJsonSchema.Object()
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
            JObject schema = McpJsonSchema.Object().String("name").Build();

            var field = (JObject)schema["properties"]!["name"]!;
            Assert.That(field.ContainsKey("description"), Is.False);
            Assert.That(field.ContainsKey("enum"), Is.False);
        }

        [Test]
        public void CollectEveryRequiredFieldInDeclarationOrder()
        {
            JObject schema = McpJsonSchema.Object()
                                           .String("first", required: true)
                                           .Integer("skipped")
                                           .Boolean("second", required: true)
                                           .Build();

            Assert.That(((JArray)schema["required"]!).ToObject<string[]>(), Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void NestAnObjectFieldWithItsOwnProperties()
        {
            JObject schema = McpJsonSchema.Object()
                                           .Object("camera", McpJsonSchema.Object().String("mode"), "The camera.", required: true)
                                           .Build();

            var camera = (JObject)schema["properties"]!["camera"]!;
            Assert.That(camera["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(camera["description"]!.Value<string>(), Is.EqualTo("The camera."));
            Assert.That(camera["properties"]!["mode"]!["type"]!.Value<string>(), Is.EqualTo("string"));
            Assert.That(((JArray)schema["required"]!).ToObject<string[]>(), Is.EqualTo(new[] { "camera" }));
        }

        [Test]
        public void AdmitNullAlongsideAnObjectForANullableNestedField()
        {
            JObject schema = McpJsonSchema.Object()
                                           .Object("scene", McpJsonSchema.Object().String("name"), nullable: true)
                                           .Build();

            var sceneType = (JArray)schema["properties"]!["scene"]!["type"]!;
            Assert.That(sceneType.ToObject<string[]>(), Is.EqualTo(new[] { "object", "null" }));
        }

        [Test]
        public void AdmitNullAlongsideTheDeclaredTypeForANullableScalar()
        {
            JObject schema = McpJsonSchema.Object().String("address", nullable: true).Build();

            var addressType = (JArray)schema["properties"]!["address"]!["type"]!;
            Assert.That(addressType.ToObject<string[]>(), Is.EqualTo(new[] { "string", "null" }));
        }

        [Test]
        public void DescribeAnArrayOfIntegerItems()
        {
            JObject schema = McpJsonSchema.Object().IntegerArray("entityIds").Build();

            var field = (JObject)schema["properties"]!["entityIds"]!;
            Assert.That(field["type"]!.Value<string>(), Is.EqualTo("array"));
            Assert.That(field["items"]!["type"]!.Value<string>(), Is.EqualTo("integer"));
        }

        [Test]
        public void ProduceAnEmptyPropertiesObjectForAnArgumentlessTool()
        {
            JObject schema = McpJsonSchema.Object().Build();

            Assert.That(schema["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(((JObject)schema["properties"]!).Count, Is.EqualTo(0));
            Assert.That(schema.ContainsKey("required"), Is.False);
        }
    }
}
