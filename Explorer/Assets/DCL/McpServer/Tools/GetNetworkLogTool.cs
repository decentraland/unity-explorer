using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests.Analytics;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class GetNetworkLogTool : McpTool
    {
        private const int DEFAULT_LIMIT = 50;
        private const int MAX_LIMIT = 200;

        // Thread-safe because the tool runs on the transport's thread-pool thread and requests can overlap.
        private static readonly ThreadSafeListPool<McpNetworkLogBuffer.Entry> ENTRIES_POOL = new (MAX_LIMIT, 2);

        private readonly McpNetworkLogBuffer buffer;

        public override string Name => "get_network_log";

        public override string Description =>
            "Read the client's recent HTTP activity — the same requests the Chrome DevTools Network domain would show. "
            + "Covers every request that flows through the shared WebRequestController (content, realm, asset bundles, textures, "
            + "audio, wearables/emotes, profiles); direct UnityWebRequest/HttpClient calls outside the controller are not captured. "
            + "Each entry has {url, method, status, mimeType, sizeBytes, durationMs, failed, reason?} and a monotonic sequence "
            + "number; pass the last seen sequence as sinceSeq to poll incrementally. Use failedOnly to see only transport failures "
            + "and HTTP error statuses (>=400), or status for an exact HTTP status.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entries to return (newest win). Default 50, max 200.")
                  .Integer("sinceSeq", "Only return entries with a sequence number greater than this.")
                  .Boolean("failedOnly", "Only return failed requests: a transport failure or an HTTP status >= 400. Default false.")
                  .Integer("status", "Only return entries with this exact HTTP status code (e.g. 404).");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        // McpNetworkLogBuffer is thread-safe and nothing else here touches ECS/Unity state, so the tool runs on the
        // transport's thread-pool thread and answers even while the main thread is busy loading or paused.
        public override bool RequiresMainThread => false;

        public GetNetworkLogTool(McpNetworkLogBuffer buffer)
        {
            this.buffer = buffer;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            long sinceSeq = arguments.GetLong("sinceSeq", -1);
            bool failedOnly = arguments.GetBool("failedOnly", false);
            int status = arguments.GetInt("status", -1);

            // Read the frontier before the copy, not after: a request landing between the two would otherwise be
            // reported in latestSeq while its entry is not in this answer, and polling from that seq would skip it.
            long latestSeq = buffer.LatestSeq;

            using var scope = ENTRIES_POOL.Get(out List<McpNetworkLogBuffer.Entry> entries);
            buffer.CopyTo(entries, sinceSeq, failedOnly, status, limit);

            var array = new JArray();

            foreach (McpNetworkLogBuffer.Entry entry in entries)
            {
                var item = new JObject
                {
                    ["seq"] = entry.Seq,
                    ["timestamp"] = entry.TimestampUtc.ToString("O"),
                    ["method"] = entry.Method,
                    ["status"] = entry.Status,
                    ["url"] = entry.Url,
                    ["mimeType"] = entry.MimeType,
                    ["sizeBytes"] = entry.SizeBytes,
                    ["durationMs"] = Math.Round(entry.DurationMs, 1),
                    ["failed"] = entry.Failed,
                };

                if (entry.FailureReason != null)
                    item["reason"] = entry.FailureReason;

                array.Add(item);
            }

            var output = new JObject
            {
                ["latestSeq"] = latestSeq,
                ["returned"] = entries.Count,
                ["entries"] = array,
            };

            return UniTask.FromResult(McpToolResult.Json(output));
        }
    }
}
