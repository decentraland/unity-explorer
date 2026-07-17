using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class McpToolsRegistryShould
    {
        [Test]
        public void BuildAnObjectSchemaWithTypedPropertiesAndRequired()
        {
            // Act
            JObject schema = McpInputSchema.Object()
                                           .Integer("count", "How many.")
                                           .String("mode", "Pick one.", enumValues: new[] { "a", "b" }, required: true)
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
            JObject schema = McpInputSchema.Object().Boolean("flag").Build();

            // Assert
            Assert.That(schema.ContainsKey("required"), Is.False);
        }

        [Test]
        public void FailRegistrationNamingTheToolWithAnInvalidSchema()
        {
            // Arrange
            var registry = new McpToolsRegistry()
               .Add(new FakeTool("broken", McpToolAnnotations.ReadOnly(), new JObject()));

            // Act & Assert
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => registry.Build());
            Assert.That(error!.Message, Does.Contain("broken"));
        }

        [Test]
        public void EmitReadOnlyAnnotationsWithoutStateChangeHints()
        {
            // Arrange
            JObject toolsList = new McpToolsRegistry()
                              .Add(new FakeTool("reader", McpToolAnnotations.ReadOnly()))
                              .Build();

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
            JObject toolsList = new McpToolsRegistry()
                              .Add(new FakeTool("mutator", McpToolAnnotations.Mutating(destructive: true, idempotent: false)))
                              .Build();

            // Act
            JObject annotations = AnnotationsOf(toolsList, "mutator");

            // Assert
            Assert.That(annotations["readOnlyHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations["destructiveHint"]!.Value<bool>(), Is.True);
            Assert.That(annotations["idempotentHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations["openWorldHint"]!.Value<bool>(), Is.False);
        }

        private static JObject AnnotationsOf(JObject toolsList, string name)
        {
            foreach (JToken entry in (JArray)toolsList["tools"]!)
                if (entry["name"]!.Value<string>() == name)
                    return (JObject)entry["annotations"]!;

            Assert.Fail($"tool '{name}' not found in tools/list");
            return null!;
        }

        private class FakeTool : IMcpTool
        {
            public string Name { get; }

            public McpToolAnnotations Annotations { get; }

            public string Description => "fake";

            public JObject InputSchema { get; }

            public FakeTool(string name, McpToolAnnotations annotations, JObject? inputSchema = null)
            {
                Name = name;
                Annotations = annotations;
                InputSchema = inputSchema ?? McpInputSchema.Object().Build();
            }

            public UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct) =>
                UniTask.FromResult(McpToolResult.Text("fake"));
        }
    }
}
