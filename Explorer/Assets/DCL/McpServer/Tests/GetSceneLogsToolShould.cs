using DCL.Diagnostics;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetSceneLogsToolShould
    {
        private SceneLogBuffer buffer = null!;
        private GetSceneLogsTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            buffer = new SceneLogBuffer();
            tool = new GetSceneLogsTool(buffer);

            buffer.Receive(new ReportLogEntry(LogType.Log, ReportCategory.JAVASCRIPT, default(SceneShortInfo), "scene says hi", null));
            buffer.Receive(new ReportLogEntry(LogType.Error, ReportCategory.UNSPECIFIED, default(SceneShortInfo), "Cannot upsert social settings", null));
            buffer.Receive(new ReportLogEntry(LogType.Log, ReportCategory.MCP, default(SceneShortInfo), "MCP server listening", null));
        }

        [Test]
        public void ReturnOnlyTheSceneOutputByDefault()
        {
            // Act
            string text = Execute(new JObject());

            // Assert
            Assert.That(text, Does.StartWith("latestSeq=2 returned=1 source=scene"));
            Assert.That(text, Does.Contain("#0 [scene] "));
            Assert.That(text, Does.Contain("scene says hi"));
            Assert.That(text, Does.Not.Contain("social settings"));
            Assert.That(text, Does.Not.Contain("[client:"));
        }

        [Test]
        public void TagClientEntriesWithTheirCategoryWhenAsked()
        {
            // Act
            string text = Execute(new JObject { ["includeClient"] = true });

            // Assert
            Assert.That(text, Does.StartWith("latestSeq=2 returned=3 source=scene+client"));
            Assert.That(text, Does.Contain("#1 [client:UNSPECIFIED] "));
            Assert.That(text, Does.Contain("#2 [client:MCP] "));
        }

        [Test]
        public void CombineTheSeverityFilterWithTheSourceFilter()
        {
            // Act
            string sceneErrors = Execute(new JObject { ["severity"] = "error" });
            string allErrors = Execute(new JObject { ["severity"] = "error", ["includeClient"] = true });

            // Assert
            Assert.That(sceneErrors, Does.Contain("returned=0"));
            Assert.That(allErrors, Does.Contain("returned=1").And.Contain("[client:UNSPECIFIED]"));
        }

        [Test]
        public void DeclareIncludeClientInItsSchema()
        {
            // Act
            JObject schema = tool.InputSchema;

            // Assert
            Assert.That(schema["properties"]?["includeClient"]?["type"]?.Value<string>(), Is.EqualTo("boolean"));
        }

        private string Execute(JObject arguments)
        {
            McpToolResult result = tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.Payload["isError"], Is.Null);
            return result.Payload["content"]?[0]?["text"]?.Value<string>() ?? string.Empty;
        }
    }
}
