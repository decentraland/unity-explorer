using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class McpToolsRegistryShould
    {
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

            public string InputSchemaJson => @"{ ""type"": ""object"", ""properties"": {} }";

            public FakeTool(string name, McpToolAnnotations annotations)
            {
                Name = name;
                Annotations = annotations;
            }

            public UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct) =>
                UniTask.FromResult(McpToolResult.Text("fake"));
        }
    }
}
