using Cysharp.Threading.Tasks;
using DCL.Mcp.Protocol;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.Mcp.Tools
{
    /// <summary>
    ///     A single MCP tool exposed to connected coding agents via tools/list and tools/call.
    /// </summary>
    public interface IMcpTool
    {
        string Name { get; }

        string Description { get; }

        /// <summary>
        ///     JSON Schema of the tool arguments, serialized. Must describe an object type.
        /// </summary>
        string InputSchemaJson { get; }

        /// <summary>
        ///     Invoked from a thread-pool thread; implementations switch to the main thread themselves
        ///     before touching ECS or Unity state. Expected failures are reported through
        ///     <see cref="McpToolResult.Error" />, not exceptions.
        /// </summary>
        UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct);
    }
}
