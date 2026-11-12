using DCL.McpServer.Core;
using DCL.McpServer.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder.Tests
{
    /// <summary>
    ///     The MCP surface of the recorder up to the point a suite needs a running world: argument diagnosis, the suite's
    ///     start, refusal and cancellation, the animations toggle and the status shape. Runs use the component seam, so
    ///     no input device is added.
    /// </summary>
    public class LocomotionMcpToolsShould
    {
        private const string SCENARIO = @"{
            ""format"": 1, ""id"": ""jog-W"", ""seed"": 1, ""fps"": 60, ""camera_yaw"": 0, ""settle"": 0.5, ""duration"": 1.0,
            ""events"": [ { ""t"": 0.0, ""fwd"": 1, ""right"": 0, ""walk"": false, ""sprint"": false } ]
        }";

        private string suitePath = null!;
        private LocomotionSuiteSession session = null!;

        [SetUp]
        public void SetUp()
        {
            suitePath = Path.Combine(Path.GetTempPath(), $"locomotion-mcp-test-{System.Guid.NewGuid():N}.json");
            File.WriteAllText(suitePath, $"[{SCENARIO}, {SCENARIO.Replace("jog-W", "jog-W-2")}]");

            session = new LocomotionSuiteSession(new LocomotionRecorderOptions(null, 1, Path.GetTempPath(), null, null, false,
                LocomotionInputSeam.Keyboard, LocomotionDeltaTime.Real, false), true);
        }

        [TearDown]
        public void TearDown()
        {
            session.Dispose();
            File.Delete(suitePath);
        }

        [Test]
        public void StayInertUntilASuiteStarts()
        {
            Assert.That(session.State, Is.EqualTo(LocomotionSuiteState.Idle));
            Assert.That(session.AnimationsEnabled, Is.True, "a recorder created for the MCP tools leaves the avatar animated");
            Assert.That(session.TryGetDrivenInput(out _), Is.False);
            Assert.That(session.TryGetHeldCameraYaw(out _), Is.False);
        }

        [Test]
        public void ReadEveryRunArgument()
        {
            var arguments = new JObject
            {
                ["suite"] = suitePath,
                ["repeats"] = 3,
                ["spawn"] = "8,0,8,90",
                ["input"] = "component",
                ["dt"] = "fixed",
                ["animations"] = true,
                ["out"] = Path.GetTempPath(),
            };

            Assert.That(RunLocomotionSuiteTool.TryParse(arguments, out LocomotionRecorderOptions? options, out string error), Is.True, error);
            Assert.That(options!.SuitePath, Is.EqualTo(suitePath));
            Assert.That(options.Repeats, Is.EqualTo(3));
            Assert.That(options.Spawn, Is.EqualTo(new Vector3(8f, 0f, 8f)));
            Assert.That(options.SpawnYaw, Is.EqualTo(90f));
            Assert.That(options.InputSeam, Is.EqualTo(LocomotionInputSeam.Component));
            Assert.That(options.DeltaTime, Is.EqualTo(LocomotionDeltaTime.Fixed));
            Assert.That(options.AnimationsEnabled, Is.True);
            Assert.That(options.OutputDirectory, Is.EqualTo(Path.GetTempPath()));
            Assert.That(options.QuitWhenDone, Is.False, "a suite started over MCP never quits the client");
        }

        [Test]
        public void DefaultToKeyboardRealTimeAndAnimationsOff()
        {
            Assert.That(RunLocomotionSuiteTool.TryParse(new JObject { ["suite"] = suitePath }, out LocomotionRecorderOptions? options, out _), Is.True);
            Assert.That(options!.Repeats, Is.EqualTo(1));
            Assert.That(options.Spawn, Is.Null);
            Assert.That(options.InputSeam, Is.EqualTo(LocomotionInputSeam.Keyboard));
            Assert.That(options.DeltaTime, Is.EqualTo(LocomotionDeltaTime.Real));
            Assert.That(options.AnimationsEnabled, Is.False);
        }

        [TestCase("suite", "relative/suite.json", "absolute")]
        [TestCase("repeats", 0, "repeats")]
        [TestCase("repeats", "3", "not a number")]
        [TestCase("spawn", "8,8", "x,y,z")]
        [TestCase("input", "Component", "lowercase")]
        [TestCase("dt", "variable", "fixed")]
        [TestCase("animations", "false", "true or false")]
        [TestCase("out", "traces", "absolute")]
        public void NameTheArgumentItCannotUse(string name, object value, string expected)
        {
            var arguments = new JObject { ["suite"] = suitePath, [name] = JToken.FromObject(value) };

            McpToolResult result = Execute(new RunLocomotionSuiteTool(session), arguments);

            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(TextOf(result), Does.Contain(expected));
            Assert.That(session.State, Is.EqualTo(LocomotionSuiteState.Idle));
        }

        [Test]
        public void StartASuiteAndReportIt()
        {
            McpToolResult result = Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["repeats"] = 2, ["input"] = "component" });
            JObject status = StructuredOf(result);

            Assert.That(status["runId"]?.Value<int>(), Is.EqualTo(1));
            Assert.That(status["state"]?.Value<string>(), Is.EqualTo("running"));
            Assert.That(status["phase"]?.Value<string>(), Is.EqualTo("waiting_for_world"));
            Assert.That(status["scenario"]?.Value<string>(), Is.EqualTo("jog-W"));
            Assert.That(status["scenarioCount"]?.Value<int>(), Is.EqualTo(2));
            Assert.That(status["runsTotal"]?.Value<int>(), Is.EqualTo(4));
            Assert.That(status["animations"]?.Value<bool>(), Is.False, "the suite's own setting is in force while it runs");
            Assert.That(status["animationsToggle"]?.Value<bool>(), Is.True);
        }

        [Test]
        public void DescribeTheStatusItReturns()
        {
            JObject status = StructuredOf(Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["input"] = "component" }));
            session.AddWrittenFile(new LocomotionTraceSummary("a.jsonl", "jog-W", 1, 60, 0, 0));
            session.AddError("the trace was not written: disk full");
            JObject withFiles = LocomotionStatusReport.Build(session);

            McpSchemaAssert.KeysMatch(LocomotionStatusReport.Schema(), status);
            McpSchemaAssert.KeysMatch(LocomotionStatusReport.Schema(), withFiles);
        }

        [Test]
        public void RefuseASecondSuiteWhileOneRuns()
        {
            Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["input"] = "component" });

            McpToolResult second = Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["input"] = "component" });

            Assert.That(second.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(TextOf(second), Does.Contain("cancel_locomotion_suite"));
            Assert.That(session.RunId, Is.EqualTo(1));
        }

        [Test]
        public void ReportASuiteThatFailsToLoad()
        {
            McpToolResult result = Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = Path.Combine(Path.GetTempPath(), "no-such-suite.json"), ["input"] = "component" });

            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(TextOf(result), Does.Contain("failed to load"));
            Assert.That(session.State, Is.EqualTo(LocomotionSuiteState.Idle));
        }

        [Test]
        public void CancelTheRunningSuite()
        {
            Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["input"] = "component" });

            JObject cancelled = StructuredOf(Execute(new CancelLocomotionSuiteTool(session), new JObject()));
            JObject again = StructuredOf(Execute(new CancelLocomotionSuiteTool(session), new JObject()));

            Assert.That(cancelled["state"]?.Value<string>(), Is.EqualTo("cancelled"));
            Assert.That(cancelled["phase"]?.Value<string>(), Is.EqualTo("idle"));
            Assert.That(cancelled["note"]?.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(again["note"]?.Value<string>(), Does.Contain("No suite"));
            Assert.That(session.TryGetDrivenInput(out _), Is.False, "a cancelled suite drives no input");
        }

        [Test]
        public void AnswerTheStatusAtOnceWhenNoSuiteRuns()
        {
            JObject status = StructuredOf(Execute(new GetLocomotionStatusTool(session), new JObject { ["waitSec"] = 20 }));

            Assert.That(status["state"]?.Value<string>(), Is.EqualTo("idle"));
            Assert.That(status["runId"]?.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void ToggleTheAnimations()
        {
            JObject off = StructuredOf(Execute(new SetAvatarAnimationsTool(session), new JObject { ["enabled"] = false }));

            Assert.That(off["animations"]?.Value<bool>(), Is.False);
            Assert.That(session.AnimationsEnabled, Is.False);
            Assert.That(session.AnimationsToggleBinding.Value, Is.False, "the debug panel's toggle follows");
            Assert.That(Execute(new SetAvatarAnimationsTool(session), new JObject()).Payload["isError"]?.Value<bool>(), Is.True);
        }

        [Test]
        public void KeepTheSuiteAnimationsUntilItEnds()
        {
            Execute(new RunLocomotionSuiteTool(session), new JObject { ["suite"] = suitePath, ["input"] = "component", ["animations"] = false });

            JObject status = StructuredOf(Execute(new SetAvatarAnimationsTool(session), new JObject { ["enabled"] = true }));

            Assert.That(status["animations"]?.Value<bool>(), Is.False);
            Assert.That(status["note"]?.Value<string>(), Does.Contain("until it ends"));

            session.Cancel();
            Assert.That(session.AnimationsEnabled, Is.True);
        }

        private static McpToolResult Execute(McpTool tool, JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private static JObject StructuredOf(McpToolResult result)
        {
            Assert.That(result.Payload["isError"], Is.Null, TextOf(result));
            return (JObject)result.Payload["structuredContent"]!;
        }

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]?[0]?["text"]?.Value<string>() ?? string.Empty;
    }
}
