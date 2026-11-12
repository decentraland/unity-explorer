using DCL.CharacterMotion.Components;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder.Tests
{
    public class LocomotionScenarioShould
    {
        private const string JOG_W = @"{
            ""format"": 1,
            ""id"": ""jog-W-hold2.0-release"",
            ""seed"": 12345,
            ""fps"": 60,               // comments are allowed, as in the format's examples
            ""camera_yaw"": 90,
            ""settle"": 1.0,
            ""duration"": 6.0,
            ""events"": [
                { ""t"": 0.0, ""fwd"": 1, ""right"": 0, ""walk"": false, ""sprint"": false },
                { ""t"": 2.0, ""fwd"": 0, ""right"": 0, ""walk"": false, ""sprint"": false }
            ]
        }";

        [Test]
        public void ParseAScenario()
        {
            LocomotionScenario scenario = LocomotionScenario.Parse(JOG_W, "test")[0];

            Assert.That(scenario.Id, Is.EqualTo("jog-W-hold2.0-release"));
            Assert.That(scenario.Seed, Is.EqualTo(12345));
            Assert.That(scenario.Fps, Is.EqualTo(60));
            Assert.That(scenario.CameraYaw, Is.EqualTo(90f));
            Assert.That(scenario.Settle, Is.EqualTo(1f));
            Assert.That(scenario.Duration, Is.EqualTo(6f));
            Assert.That(scenario.Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void ParseAnArrayOfScenarios()
        {
            List<LocomotionScenario> scenarios = LocomotionScenario.Parse($"[{JOG_W}, {JOG_W.Replace("jog-W-hold2.0-release", "second")}]", "test");

            Assert.That(scenarios.Count, Is.EqualTo(2));
            Assert.That(scenarios[1].Id, Is.EqualTo("second"));
        }

        [TestCase(-0.1f, 0)]
        [TestCase(0f, 1)]
        [TestCase(1.99f, 1)]
        [TestCase(2f, 0)]
        [TestCase(10f, 0)]
        public void HoldEachEventUntilTheNext(float t, int expectedFwd)
        {
            LocomotionScenario scenario = LocomotionScenario.Parse(JOG_W, "test")[0];

            Assert.That(scenario.SampleAt(t).Fwd, Is.EqualTo(expectedFwd));
        }

        [TestCase("\"fwd\": 1", "\"fwd\": 2")]
        [TestCase("\"t\": 2.0", "\"t\": -1.0")]
        [TestCase("\"format\": 1", "\"format\": 2")]
        [TestCase("\"fps\": 60", "\"fps\": 0")]
        [TestCase("\"id\": \"jog-W-hold2.0-release\"", "\"id\": \"a/b\"")]
        public void RejectAnInvalidScenario(string valid, string invalid)
        {
            Assert.Throws<FormatException>(() => LocomotionScenario.Parse(JOG_W.Replace(valid, invalid), "test"));
        }

        [TestCase(0f, 0f, 1f, 0f)]
        [TestCase(1f, 0f, 0f, 90f)]
        [TestCase(0f, 0f, -1f, 180f)]
        [TestCase(-1f, 0f, 0f, -90f)]
        public void MeasureYawClockwiseFromNorth(float x, float y, float z, float expected)
        {
            Assert.That(LocomotionSuiteSystem.Yaw(new Vector3(x, y, z)), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void ParseASpawnWithAndWithoutYaw()
        {
            Assert.That(LocomotionRecorderOptions.TryParseSpawn("8,0.5,8", out Vector3? spawn, out float? yaw), Is.True);
            Assert.That(spawn, Is.EqualTo(new Vector3(8f, 0.5f, 8f)));
            Assert.That(yaw, Is.Null);

            Assert.That(LocomotionRecorderOptions.TryParseSpawn("8,0,8,-90", out _, out yaw), Is.True);
            Assert.That(yaw, Is.EqualTo(-90f));

            Assert.That(LocomotionRecorderOptions.TryParseSpawn("8,8", out _, out _), Is.False);
        }

        [Test]
        public void CountConsumedInputAgainstTheScenario()
        {
            var running = new LocomotionFrameSample { Fwd = 0.7071f, Right = 0.7071f, Kind = MovementKind.Run, Expected = new LocomotionInputStep(0f, 1, 1, false, true) };
            var lagging = new LocomotionFrameSample { Fwd = 0f, Right = 0f, Kind = MovementKind.Idle, Expected = new LocomotionInputStep(0f, 1, 0, false, false) };

            Assert.That(running.MatchesExpectedInput, Is.True);
            Assert.That(lagging.MatchesExpectedInput, Is.False);
        }
    }
}
