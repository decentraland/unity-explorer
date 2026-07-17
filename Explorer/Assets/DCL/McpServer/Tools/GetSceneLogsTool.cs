using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class GetSceneLogsTool : IMcpTool
    {
        private const int DEFAULT_LIMIT = 100;
        private const int MAX_LIMIT = 500;

        private readonly SceneLogBuffer logBuffer;

        public string Name => "get_scene_logs";

        public McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public string Description =>
            "Read the scene's JavaScript console output (logs, warnings, errors and exceptions). Entries carry monotonic sequence numbers; "
            + "pass the last seen sequence as sinceSeq to poll incrementally.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""limit"": { ""type"": ""integer"", ""description"": ""Maximum entries to return (newest win). Default 100."" },
                    ""severity"": { ""type"": ""string"", ""enum"": [""all"", ""error""], ""description"": ""Filter by severity. Default all."" },
                    ""sinceSeq"": { ""type"": ""integer"", ""description"": ""Only return entries with a sequence number greater than this."" }
                }
            }";

        public GetSceneLogsTool(SceneLogBuffer logBuffer)
        {
            this.logBuffer = logBuffer;
        }

        public UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            bool errorsOnly = arguments.GetString("severity", "all") == "error";
            long sinceSeq = arguments.GetLong("sinceSeq", -1);

            var entries = new List<SceneLogBuffer.Entry>(limit);
            logBuffer.CopyTo(entries, sinceSeq, errorsOnly, limit);

            var output = new StringBuilder();
            output.AppendLine($"latestSeq={logBuffer.LatestSeq} returned={entries.Count}");

            foreach (SceneLogBuffer.Entry entry in entries)
                output.AppendLine($"#{entry.Seq} {entry.Message}");

            return UniTask.FromResult(McpToolResult.Text(output.ToString()));
        }
    }
}
