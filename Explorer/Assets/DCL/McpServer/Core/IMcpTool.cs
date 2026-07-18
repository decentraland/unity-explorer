using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     A single MCP tool exposed to connected coding agents via tools/list and tools/call.
    /// </summary>
    public interface IMcpTool
    {
        string Name { get; }

        string Description { get; }

        /// <summary>
        ///     JSON Schema of the tool arguments. Must be a JSON Schema object (type=object); build it with
        ///     <see cref="McpJsonSchema" /> so a malformed schema is caught at registration, not on first use.
        /// </summary>
        JObject InputSchema { get; }

        /// <summary>
        ///     Behaviour hints (read-only, destructive, idempotent, open-world) surfaced in tools/list.
        /// </summary>
        McpToolAnnotations Annotations { get; }

        /// <summary>
        ///     JSON Schema of this tool's structuredContent, surfaced as outputSchema in tools/list. Build it with
        ///     <see cref="McpJsonSchema" />. Null (the default) when the tool returns only unstructured text; tools
        ///     that emit <see cref="McpToolResult.TextWithStructured" /> override this to describe that payload.
        /// </summary>
        JObject? OutputSchema => null;

        /// <summary>
        ///     Invoked from a thread-pool thread; implementations switch to the main thread themselves
        ///     before touching ECS or Unity state. Expected failures are reported through
        ///     <see cref="McpToolResult.Error" />, not exceptions.
        /// </summary>
        UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct);
    }
}
