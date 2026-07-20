using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     A single MCP tool exposed to connected coding agents via tools/list and tools/call.
    ///     The abstract members are the required contract; the virtual ones (<see cref="DescribeInput" />,
    ///     <see cref="OutputSchema" />, <see cref="requiresMainThread" />) are optional overrides whose defaults
    ///     describe a tool that takes no arguments, returns unstructured text, runs on the main thread.
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
        ///     Runs the tool: hops to the main thread first unless <see cref="requiresMainThread" /> opts out.
        ///     The transport invokes this from a thread-pool thread.
        /// </summary>
        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (requiresMainThread)
                await UniTask.SwitchToMainThread(ct);

            return await ExecuteCoreAsync(arguments, ct);
        }

        /// <summary>
        ///     Optional override: JSON Schema of this tool's structuredContent, surfaced as outputSchema in
        ///     tools/list. Build it with <see cref="McpJsonSchema" />. Null (the default) when the tool returns
        ///     only unstructured text; tools that emit <see cref="McpToolResult.TextWithStructured" /> override
        ///     this to describe that payload.
        /// </summary>
        public virtual JObject? OutputSchema => null;

        /// <summary>
        ///     Optional override: false lets the tool run directly on the transport's thread-pool thread, so it
        ///     answers even while the main thread is busy or paused. Only for tools that touch neither ECS nor
        ///     Unity state and read exclusively thread-safe sources.
        /// </summary>
        protected virtual bool requiresMainThread => true;

        /// <summary>
        ///     The tool body. Starts on the main thread (unless <see cref="requiresMainThread" /> opted out), so
        ///     implementations may touch ECS and Unity state directly. Offload heavy CPU work to the thread pool
        ///     yourself and hop back before touching that state again. Expected failures are reported through
        ///     <see cref="McpToolResult.Error" />, not exceptions.
        /// </summary>
        protected abstract UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct);

        /// <summary>
        ///     Optional override: declares the tool's argument fields on the provided builder. The default
        ///     describes a tool without arguments (an empty object schema).
        /// </summary>
        protected virtual McpJsonSchema DescribeInput(McpJsonSchema schema) => schema;
    }
}
