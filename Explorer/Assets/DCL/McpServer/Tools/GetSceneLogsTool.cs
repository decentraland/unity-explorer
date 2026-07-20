using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class GetSceneLogsTool : McpTool
    {
        private const int DEFAULT_LIMIT = 100;
        private const int MAX_LIMIT = 500;

        private readonly SceneLogBuffer logBuffer;

        public override string Name => "get_scene_logs";

        public override string Description =>
            "Read the scene's JavaScript console output (logs, warnings, errors and exceptions). Entries carry monotonic sequence numbers; "
            + "pass the last seen sequence as sinceSeq to poll incrementally.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entries to return (newest win). Default 100.")
                  .String("severity", "Filter by severity. Default all.", enumValues: new[] { "all", "error" })
                  .Integer("sinceSeq", "Only return entries with a sequence number greater than this.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        // SceneLogBuffer is thread-safe and nothing else here touches ECS/Unity state, so the tool runs on the
        // transport's thread-pool thread and answers even while the main thread is busy loading or paused.
        protected override bool requiresMainThread => false;

        public GetSceneLogsTool(SceneLogBuffer logBuffer)
        {
            this.logBuffer = logBuffer;
        }

        protected override UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct)
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
