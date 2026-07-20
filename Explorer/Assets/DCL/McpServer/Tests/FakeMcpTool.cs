using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     A configurable <see cref="McpTool" /> test double: its ExecuteAsync returns a preset result,
    ///     or throws a preset exception, and records the arguments and cancellation token it was called with.
    ///     Keeps the routing tests independent of the real tool implementations.
    /// </summary>
    internal sealed class FakeMcpTool : McpTool
    {
        private readonly Func<JObject, CancellationToken, McpToolResult> execute;

        public override string Name { get; }

        public override string Description { get; }

        public override McpToolAnnotations Annotations { get; }

        public int CallCount { get; private set; }

        public JObject? LastArguments { get; private set; }

        private FakeMcpTool(string name, string description, McpToolAnnotations annotations,
            Func<JObject, CancellationToken, McpToolResult> execute)
        {
            Name = name;
            Description = description;
            Annotations = annotations;
            this.execute = execute;
        }

        /// <summary>A tool whose ExecuteAsync returns <paramref name="result" /> (defaults to a text result).</summary>
        public static FakeMcpTool Returning(string name, McpToolResult? result = null,
            McpToolAnnotations? annotations = null)
        {
            McpToolResult toReturn = result ?? McpToolResult.Text($"{name} ran");
            return new FakeMcpTool(name, $"{name} description",
                annotations ?? McpToolAnnotations.ReadOnly(), (_, _) => toReturn);
        }

        /// <summary>A tool whose ExecuteAsync throws <paramref name="exception" />.</summary>
        public static FakeMcpTool Throwing(string name, Exception exception,
            McpToolAnnotations? annotations = null) =>
            new (name, $"{name} description",
                annotations ?? McpToolAnnotations.ReadOnly(), (_, _) => throw exception);

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("value", "Any value.");

        protected override UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct)
        {
            CallCount++;
            LastArguments = arguments;
            return UniTask.FromResult(execute(arguments, ct));
        }
    }
}
