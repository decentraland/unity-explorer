using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class McpToolsRegistryShould
    {
        [Test]
        public void BuildAnObjectSchemaWithTypedPropertiesAndRequired()
        {
            // Act
            JObject schema = McpJsonSchema.Object()
                                           .Integer("count", "How many.")
                                           .String("mode", "Pick one.", enumValues: new[] { "a", "b" }, isRequired: true)
                                           .Build();

            // Assert
            Assert.That(schema["type"]!.Value<string>(), Is.EqualTo("object"));

            var properties = (JObject)schema["properties"]!;
            Assert.That(properties["count"]!["type"]!.Value<string>(), Is.EqualTo("integer"));
            Assert.That(properties["count"]!["description"]!.Value<string>(), Is.EqualTo("How many."));
            Assert.That(properties["mode"]!["type"]!.Value<string>(), Is.EqualTo("string"));
            Assert.That(((JArray)properties["mode"]!["enum"]!).ToObject<string[]>(), Is.EqualTo(new[] { "a", "b" }));

            Assert.That(((JArray)schema["required"]!).ToObject<string[]>(), Is.EqualTo(new[] { "mode" }));
        }

        [Test]
        public void OmitRequiredWhenNoFieldIsRequired()
        {
            // Act
            JObject schema = McpJsonSchema.Object().Boolean("flag").Build();

            // Assert
            Assert.That(schema.ContainsKey("required"), Is.False);
        }

        [Test]
        public void FailRegistrationNamingTheToolWithAnInvalidOutputSchema()
        {
            // Arrange
            var registry = new McpToolsRegistry()
               .Add(new FakeTool("broken", McpToolAnnotations.ReadOnly(), outputSchema: new JObject()));

            // Act & Assert
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => registry.Build());
            Assert.That(error.Message, Does.Contain("broken"));
        }

        [Test]
        public void EmitReadOnlyAnnotationsWithoutStateChangeHints()
        {
            // Arrange
            JObject toolsList = Payload(new McpToolsRegistry()
                                       .Add(new FakeTool("reader", McpToolAnnotations.ReadOnly()))
                                       .Build());

            // Act
            JObject annotations = AnnotationsOf(toolsList, "reader");

            // Assert
            Assert.That(annotations["readOnlyHint"]!.Value<bool>(), Is.True);
            Assert.That(annotations["openWorldHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations.ContainsKey("destructiveHint"), Is.False);
            Assert.That(annotations.ContainsKey("idempotentHint"), Is.False);
        }

        [Test]
        public void EmitMutatingAnnotationsWithAllStateChangeHints()
        {
            // Arrange
            JObject toolsList = Payload(new McpToolsRegistry()
                                       .Add(new FakeTool("mutator", McpToolAnnotations.Mutating(destructive: true, idempotent: false)))
                                       .Build());

            // Act
            JObject annotations = AnnotationsOf(toolsList, "mutator");

            // Assert
            Assert.That(annotations["readOnlyHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations["destructiveHint"]!.Value<bool>(), Is.True);
            Assert.That(annotations["idempotentHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations["openWorldHint"]!.Value<bool>(), Is.False);
        }

        [Test]
        public void IncludeOutputSchemaWhenTheToolDeclaresOne()
        {
            // Arrange
            JObject outputSchema = McpJsonSchema.Object().Integer("total").Build();

            JObject toolsList = Payload(new McpToolsRegistry()
                                       .Add(new FakeTool("structured", McpToolAnnotations.ReadOnly(), outputSchema: outputSchema))
                                       .Build());

            // Act
            JObject entry = EntryOf(toolsList, "structured");

            // Assert
            Assert.That(entry.ContainsKey("outputSchema"), Is.True);
            Assert.That(entry["outputSchema"]!["type"]!.Value<string>(), Is.EqualTo("object"));
        }

        [Test]
        public void OmitOutputSchemaWhenTheToolDeclaresNone()
        {
            // Arrange
            JObject toolsList = Payload(new McpToolsRegistry()
                                       .Add(new FakeTool("plain", McpToolAnnotations.ReadOnly()))
                                       .Build());

            // Act & Assert
            Assert.That(EntryOf(toolsList, "plain").ContainsKey("outputSchema"), Is.False);
        }

        [Test]
        public void FindARegisteredToolByName()
        {
            var tool = new FakeTool("known", McpToolAnnotations.ReadOnly());
            var registry = new McpToolsRegistry().Add(tool);

            bool found = registry.TryGet("known", out McpTool? resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(tool));
        }

        [Test]
        public void NotFindAnUnknownTool()
        {
            var registry = new McpToolsRegistry().Add(new FakeTool("known", McpToolAnnotations.ReadOnly()));

            Assert.That(registry.TryGet("missing", out McpTool? resolved), Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void NotFindAToolForANullOrEmptyName()
        {
            var registry = new McpToolsRegistry().Add(new FakeTool("known", McpToolAnnotations.ReadOnly()));

            Assert.That(registry.TryGet(null, out McpTool? byNull), Is.False);
            Assert.That(byNull, Is.Null);

            Assert.That(registry.TryGet(string.Empty, out McpTool? byEmpty), Is.False);
            Assert.That(byEmpty, Is.Null);
        }

        [Test]
        public void ReflectTheRegisteredSetInTheToolsList()
        {
            JObject toolsList = Payload(new McpToolsRegistry()
                                       .Add(new FakeTool("first", McpToolAnnotations.ReadOnly()))
                                       .Add(new FakeTool("second", McpToolAnnotations.Mutating(destructive: false, idempotent: true)))
                                       .Build());

            var names = new List<string>();

            foreach (JToken entry in (JArray)toolsList["tools"]!)
                names.Add(entry["name"]!.Value<string>()!);

            Assert.That(names, Is.EquivalentTo(new[] { "first", "second" }));
        }

        private static JObject Payload(McpToolsRegistry registry) =>
            JObject.Parse(registry.ToolsListPayload().ToString());

        private static JObject AnnotationsOf(JObject toolsList, string name) =>
            (JObject)EntryOf(toolsList, name)["annotations"]!;

        private static JObject EntryOf(JObject toolsList, string name)
        {
            foreach (JToken entry in (JArray)toolsList["tools"]!)
                if (entry["name"]!.Value<string>() == name)
                    return (JObject)entry;

            Assert.Fail($"tool '{name}' not found in tools/list");
            return null!;
        }

        private class FakeTool : McpTool
        {
            public override string Name { get; }

            public override McpToolAnnotations Annotations { get; }

            public override string Description => "fake";

            public override JObject? OutputSchema { get; }

            public FakeTool(string name, McpToolAnnotations annotations, JObject? outputSchema = null)
            {
                Name = name;
                Annotations = annotations;
                OutputSchema = outputSchema;
            }

            protected override UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct) =>
                UniTask.FromResult(McpToolResult.Text("fake"));
        }
    }
}
