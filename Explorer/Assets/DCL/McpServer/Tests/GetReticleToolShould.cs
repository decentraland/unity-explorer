using Arch.Core;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetReticleToolShould
    {
        private World world = null!;
        private SyntheticInputAgent agent = null!;
        private GetReticleTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            agent = new SyntheticInputAgent(world, world.Create());
            tool = new GetReticleTool(agent);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        private McpToolResult Execute() =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();

        private static ReticleReading SceneEntityReading()
        {
            ReticleReading reading = ReticleReading.Empty();
            reading.Kind = ReticleTargetKind.SceneEntity;
            reading.SceneEntityId = 12;
            reading.CrdtEntityId = 530;
            reading.HoverText = "Open";
            reading.Tooltips = new[] { "E Open" };
            reading.WithinRange = true;
            reading.HasPointerEvents = true;
            reading.DistanceToCamera = 3f;
            reading.DistanceToPlayer = 2.5f;
            reading.PointerImagePoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            reading.Ray = new Ray(Vector3.zero, Vector3.forward);
            return reading;
        }

        [Test]
        public void ErrorWhenTheReaderIsNotInstalled()
        {
            McpToolResult result = Execute();

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("not installed"));
        }

        [Test]
        public void ErrorWhenThePipelineIsIdle()
        {
            agent.InstallReticleProbe(static () => ReticleReading.Unavailable("the cursor is panning the camera"));

            McpToolResult result = Execute();

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("panning"));
        }

        [Test]
        public void ShapeASceneEntityHitLikeThePointerResults()
        {
            agent.InstallReticleProbe(static () => SceneEntityReading());

            McpToolResult result = Execute();

            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["kind"]!.ToString(), Is.EqualTo("scene_entity"));
            Assert.That(structured["entityId"]!.Value<int>(), Is.EqualTo(12));
            Assert.That(structured["crdtEntityId"]!.Value<int>(), Is.EqualTo(530));
            Assert.That(structured["hoverText"]!.ToString(), Is.EqualTo("Open"));
            Assert.That(structured["withinRange"]!.Value<bool>(), Is.True);
            Assert.That(structured["cursor"]!["center"]!["x"]!.Value<float>(), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(structured["screen"]!["width"]!.Value<int>(), Is.EqualTo(Screen.width));
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("crdtId 530"));
        }

        [Test]
        public void FlagACursorOutsideTheWindow()
        {
            agent.InstallReticleProbe(static () =>
            {
                ReticleReading reading = SceneEntityReading();
                reading.PointerImagePoint = new Vector2(-604f, 1175f);
                return reading;
            });

            McpToolResult result = Execute();

            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["cursor"]!["onScreen"]!.Value<bool>(), Is.False);
            Assert.That(structured["cursor"]!["center"]!["x"]!.Value<float>(), Is.EqualTo(0f), "the center is clamped, which is why the flag exists");
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("outside the window"));
        }

        [Test]
        public void ReportTheCursorOnScreenWhenItIs()
        {
            agent.InstallReticleProbe(static () => SceneEntityReading());

            var structured = (JObject)Execute().Payload["structuredContent"]!;
            Assert.That(structured["cursor"]!["onScreen"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            agent.InstallReticleProbe(static () => SceneEntityReading());

            McpToolResult result = Execute();

            McpSchemaAssert.KeysMatch(tool.OutputSchema, (JObject)result.Payload["structuredContent"]!);
        }

        [Test]
        public void ReadWithoutProducingAnyInput()
        {
            Assert.That(tool.Annotations.ToJObject()["readOnlyHint"]!.Value<bool>(), Is.True);
            Assert.That(tool.InputSchema["properties"]!.HasValues, Is.False);
        }
    }
}
