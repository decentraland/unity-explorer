using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     A configurable <see cref="IMcpTool" /> test double: its ExecuteAsync returns a preset result,
    ///     or throws a preset exception, and records the arguments and cancellation token it was called with.
    ///     Keeps the routing tests independent of the real tool implementations.
    /// </summary>
    internal sealed class FakeMcpTool : IMcpTool
    {
        private readonly Func<JObject, CancellationToken, McpToolResult> execute;

        public string Name { get; }

        public string Description { get; }

        public JObject InputSchema { get; }

        public McpToolAnnotations Annotations { get; }

        public int CallCount { get; private set; }

        public JObject? LastArguments { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        private FakeMcpTool(string name, string description, JObject inputSchema, McpToolAnnotations annotations,
            Func<JObject, CancellationToken, McpToolResult> execute)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Annotations = annotations;
            this.execute = execute;
        }

        /// <summary>A tool whose ExecuteAsync returns <paramref name="result" /> (defaults to a text result).</summary>
        public static FakeMcpTool Returning(string name, McpToolResult? result = null, JObject? inputSchema = null,
            McpToolAnnotations? annotations = null)
        {
            McpToolResult toReturn = result ?? McpToolResult.Text($"{name} ran");
            return new FakeMcpTool(name, $"{name} description", inputSchema ?? DefaultSchema(),
                annotations ?? McpToolAnnotations.ReadOnly(), (_, _) => toReturn);
        }

        /// <summary>A tool whose ExecuteAsync throws <paramref name="exception" />.</summary>
        public static FakeMcpTool Throwing(string name, Exception exception, JObject? inputSchema = null,
            McpToolAnnotations? annotations = null) =>
            new (name, $"{name} description", inputSchema ?? DefaultSchema(),
                annotations ?? McpToolAnnotations.ReadOnly(), (_, _) => throw exception);

        private static JObject DefaultSchema() =>
            McpInputSchema.Object().String("value", "Any value.").Build();

        public UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            CallCount++;
            LastArguments = arguments;
            LastCancellationToken = ct;
            return UniTask.FromResult(execute(arguments, ct));
        }
    }
}
