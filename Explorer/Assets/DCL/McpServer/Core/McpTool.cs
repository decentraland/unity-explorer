using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     A single MCP tool exposed to connected coding agents via tools/list and tools/call.
    /// </summary>
    public abstract class McpTool
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        /// <summary>
        ///     JSON Schema of the tool arguments, surfaced as inputSchema in tools/list. Assembled from
        ///     <see cref="DescribeInput" />, so it is a valid JSON Schema object (type=object) by construction.
        /// </summary>
        public JObject InputSchema => DescribeInput(McpJsonSchema.Object()).Build();

        /// <summary>
        ///     Behaviour hints (read-only, destructive, idempotent, open-world) surfaced in tools/list.
        /// </summary>
        public abstract McpToolAnnotations Annotations { get; }

        /// <summary>
        ///     Invoked on the main thread, so implementations may touch ECS and Unity state directly. Offload
        ///     heavy CPU work to the thread pool yourself and hop back before touching that state again. Expected
        ///     failures are reported through <see cref="McpToolResult.Error" />, not exceptions.
        /// </summary>
        public abstract UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct);

        /// <summary>
        ///     JSON Schema of this tool's structuredContent, surfaced as outputSchema in tools/list. Build it with
        ///     <see cref="McpJsonSchema" />. Null (the default) when the tool returns only unstructured text; tools
        ///     that emit <see cref="McpToolResult.TextWithStructured" /> override this to describe that payload.
        /// </summary>
        public virtual JObject? OutputSchema => null;

        /// <summary>
        ///     Declares the tool's argument fields on the provided builder. The default describes a tool
        ///     without arguments (an empty object schema).
        /// </summary>
        protected virtual McpJsonSchema DescribeInput(McpJsonSchema schema) => schema;
    }
}
