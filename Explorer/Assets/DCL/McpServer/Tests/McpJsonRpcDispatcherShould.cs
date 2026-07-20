using DCL.McpServer.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.McpServer.Tests
{
    public class McpJsonRpcDispatcherShould
    {
        private const string SERVER_VERSION = "9.9.9-test";

        [Test]
        public void AnswerInitializeWithProtocolCapabilitiesAndServerInfo()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            JObject result = ResultOf(Dispatch(dispatcher, Request(1, "initialize")));

            Assert.That(result["protocolVersion"]!.Value<string>(), Is.EqualTo("2025-06-18"));
            Assert.That(result["protocolVersion"]!.Value<string>(), Is.EqualTo(McpJsonRpcDispatcher.PROTOCOL_VERSION));

            var capabilities = (JObject)result["capabilities"]!;
            Assert.That(capabilities.ContainsKey("tools"), Is.True);

            var serverInfo = (JObject)result["serverInfo"]!;
            Assert.That(serverInfo["version"]!.Value<string>(), Is.EqualTo(SERVER_VERSION));
            Assert.That(serverInfo.ContainsKey("pid"), Is.True);
            Assert.That(serverInfo["pid"]!.Type, Is.EqualTo(JTokenType.Integer));
        }

        [Test]
        public void AnswerPingWithAnEmptyResult()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            JObject result = ResultOf(Dispatch(dispatcher, Request(2, "ping")));

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void ListToolsWithInputSchemaAndAnnotations()
        {
            FakeMcpTool tool = FakeMcpTool.Returning("reader", annotations: McpToolAnnotations.ReadOnly());
            McpJsonRpcDispatcher dispatcher = DispatcherWith(tool);

            JObject result = ResultOf(Dispatch(dispatcher, Request(3, "tools/list")));

            var tools = (JArray)result["tools"]!;
            Assert.That(tools.Count, Is.EqualTo(1));

            JObject entry = ToolEntry(result, "reader");
            Assert.That(entry["description"]!.Value<string>(), Is.EqualTo("reader description"));

            var inputSchema = (JObject)entry["inputSchema"]!;
            Assert.That(inputSchema["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(inputSchema.ContainsKey("properties"), Is.True);

            var annotations = (JObject)entry["annotations"]!;
            Assert.That(annotations["readOnlyHint"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void CallAKnownToolAndWrapItsResult()
        {
            FakeMcpTool tool = FakeMcpTool.Returning("echo", McpToolResult.Text("done"));
            McpJsonRpcDispatcher dispatcher = DispatcherWith(tool);

            var arguments = new JObject { ["value"] = "hi" };
            JObject result = ResultOf(Dispatch(dispatcher, CallRequest(4, "echo", arguments)));

            Assert.That(tool.CallCount, Is.EqualTo(1));
            Assert.That(tool.LastArguments!["value"]!.Value<string>(), Is.EqualTo("hi"));

            var content = (JArray)result["content"]!;
            Assert.That(content[0]["type"]!.Value<string>(), Is.EqualTo("text"));
            Assert.That(content[0]["text"]!.Value<string>(), Is.EqualTo("done"));
            Assert.That(result.ContainsKey("isError"), Is.False);
        }

        [Test]
        public void CallWithEmptyArgumentsWhenNoneAreProvided()
        {
            FakeMcpTool tool = FakeMcpTool.Returning("echo");
            McpJsonRpcDispatcher dispatcher = DispatcherWith(tool);

            // A tools/call request carrying a name but no arguments object.
            string request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 5,
                ["method"] = "tools/call",
                ["params"] = new JObject { ["name"] = "echo" },
            }.ToString(Formatting.None);

            ResultOf(Dispatch(dispatcher, request));

            Assert.That(tool.CallCount, Is.EqualTo(1));
            Assert.That(tool.LastArguments, Is.Not.Null);
            Assert.That(tool.LastArguments!.Count, Is.EqualTo(0));
        }

        [Test]
        public void RejectAnUnknownToolWithInvalidParams()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith(FakeMcpTool.Returning("known"));

            JObject error = ErrorOf(Dispatch(dispatcher, CallRequest(6, "missing", new JObject())));

            Assert.That(error["code"]!.Value<int>(), Is.EqualTo(-32602));
            Assert.That(error["message"]!.Value<string>(), Does.Contain("missing"));
        }

        [Test]
        public void ReportAToolFailureAsAnIsErrorResultNotAJsonRpcError()
        {
            FakeMcpTool tool = FakeMcpTool.Throwing("boom", new InvalidOperationException("kaboom"));
            McpJsonRpcDispatcher dispatcher = DispatcherWith(tool);

            // ReportHub routes the caught exception to the Unity logger; expect it so the test does not fail.
            LogAssert.Expect(LogType.Exception, new Regex("kaboom"));

            JObject response = JObject.Parse(Dispatch(dispatcher, CallRequest(7, "boom", new JObject()))!);

            // The failure is delivered inside result (isError), not as a top-level JSON-RPC error.
            Assert.That(response.ContainsKey("error"), Is.False);

            var result = (JObject)response["result"]!;
            Assert.That(result["isError"]!.Value<bool>(), Is.True);
            Assert.That(result["content"]![0]!["text"]!.Value<string>(), Does.Contain("boom"));
        }

        [Test]
        public void RethrowWhenAToolIsCancelled()
        {
            FakeMcpTool tool = FakeMcpTool.Throwing("cancelled", new OperationCanceledException());
            McpJsonRpcDispatcher dispatcher = DispatcherWith(tool);

            Assert.Throws<OperationCanceledException>(() => Dispatch(dispatcher, CallRequest(8, "cancelled", new JObject())));
        }

        [Test]
        public void RejectAnUnknownMethodWithMethodNotFound()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            JObject error = ErrorOf(Dispatch(dispatcher, Request(9, "resources/list")));

            Assert.That(error["code"]!.Value<int>(), Is.EqualTo(-32601));
            Assert.That(error["message"]!.Value<string>(), Does.Contain("resources/list"));
        }

        [Test]
        public void DropNotificationsWithoutAnId()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            string request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized",
            }.ToString(Formatting.None);

            Assert.That(Dispatch(dispatcher, request), Is.Null);
        }

        [Test]
        public void ReplyWithAParseErrorOnMalformedJson()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            JObject error = ErrorOf(Dispatch(dispatcher, "{ this is not json"));

            Assert.That(error["code"]!.Value<int>(), Is.EqualTo(-32700));
        }

        [Test]
        public void RejectARequestMissingAMethodWithInvalidRequest()
        {
            McpJsonRpcDispatcher dispatcher = DispatcherWith();

            string request = new JObject { ["jsonrpc"] = "2.0", ["id"] = 10 }.ToString(Formatting.None);

            JObject error = ErrorOf(Dispatch(dispatcher, request));

            Assert.That(error["code"]!.Value<int>(), Is.EqualTo(-32600));
        }

        private static McpJsonRpcDispatcher DispatcherWith(params McpTool[] tools)
        {
            var registry = new McpToolsRegistry();

            foreach (McpTool tool in tools)
                registry.Add(tool);

            registry.Build();
            return new McpJsonRpcDispatcher(registry, SERVER_VERSION);
        }

        private static string? Dispatch(McpJsonRpcDispatcher dispatcher, string requestJson) =>
            dispatcher.DispatchAsync(requestJson, CancellationToken.None).GetAwaiter().GetResult();

        private static string Request(int id, string method) =>
            new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
            }.ToString(Formatting.None);

        private static string CallRequest(int id, string toolName, JObject arguments) =>
            new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments,
                },
            }.ToString(Formatting.None);

        private static JObject ResultOf(string? response)
        {
            Assert.That(response, Is.Not.Null, "expected a response, got a dropped message");
            var parsed = JObject.Parse(response!);
            Assert.That(parsed["jsonrpc"]!.Value<string>(), Is.EqualTo("2.0"));
            Assert.That(parsed.ContainsKey("error"), Is.False, $"expected a result, got an error: {response}");
            return (JObject)parsed["result"]!;
        }

        private static JObject ErrorOf(string? response)
        {
            Assert.That(response, Is.Not.Null, "expected an error response, got a dropped message");
            var parsed = JObject.Parse(response!);
            Assert.That(parsed["jsonrpc"]!.Value<string>(), Is.EqualTo("2.0"));
            return (JObject)parsed["error"]!;
        }

        private static JObject ToolEntry(JObject toolsListResult, string name)
        {
            foreach (JToken entry in (JArray)toolsListResult["tools"]!)
                if (entry["name"]!.Value<string>() == name)
                    return (JObject)entry;

            Assert.Fail($"tool '{name}' not found in tools/list");
            return null!;
        }
    }
}
