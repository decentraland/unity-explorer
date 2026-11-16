using DCL.CharacterMotion.Components;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
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

        private const string JUMP_TAP = @"{
            ""format"": 1,
            ""id"": ""jump-tap0.1"",
            ""fps"": 60,
            ""settle"": 1.0,
            ""duration"": 3.0,
            ""events"": [
                { ""t"": 0.5, ""fwd"": 0, ""right"": 0, ""jump"": true },
                { ""t"": 0.6, ""fwd"": 0, ""right"": 0, ""jump"": false }
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

        [Test]
        public void ReadJumpAsAHeldKeyThatDefaultsToReleased()
        {
            LocomotionScenario withoutJump = LocomotionScenario.Parse(JOG_W, "test")[0];
            LocomotionScenario tap = LocomotionScenario.Parse(JUMP_TAP, "test")[0];

            Assert.That(withoutJump.SampleAt(1f).Jump, Is.False);
            Assert.That(tap.SampleAt(0.49f).Jump, Is.False);
            Assert.That(tap.SampleAt(0.5f).Jump, Is.True);
            Assert.That(tap.SampleAt(0.59f).Jump, Is.True);
            Assert.That(tap.SampleAt(0.6f).Jump, Is.False);
            Assert.That(tap.SampleAt(0.5f).IsMoving, Is.False);
        }

        [TestCase("\"jump\": true", "\"jump\": 1")]
        [TestCase("\"jump\": true", "\"jump\": \"true\"")]
        public void RejectAJumpThatIsNotABoolean(string valid, string invalid)
        {
            Assert.Throws<FormatException>(() => LocomotionScenario.Parse(JUMP_TAP.Replace(valid, invalid), "test"));
        }

        [TestCase("\"fwd\": 1", "\"fwd\": 2")]
        [TestCase("\"walk\": false", "\"walk\": 0")]
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

            var jumping = new LocomotionFrameSample { Kind = MovementKind.Idle, Jump = true, Expected = new LocomotionInputStep(0f, 0, 0, false, false, jump: true) };
            var jumpNotSeen = new LocomotionFrameSample { Kind = MovementKind.Idle, Jump = false, Expected = new LocomotionInputStep(0f, 0, 0, false, false, jump: true) };
            var jumpStillHeld = new LocomotionFrameSample { Kind = MovementKind.Idle, Jump = true, Expected = LocomotionInputStep.IDLE };

            Assert.That(running.MatchesExpectedInput, Is.True);
            Assert.That(lagging.MatchesExpectedInput, Is.False);
            Assert.That(jumping.MatchesExpectedInput, Is.True);
            Assert.That(jumpNotSeen.MatchesExpectedInput, Is.False);
            Assert.That(jumpStillHeld.MatchesExpectedInput, Is.False);
        }

        [Test]
        public void WriteTheJumpKeyInEachFrameAndCountItsMismatches()
        {
            LocomotionScenario tap = LocomotionScenario.Parse(JUMP_TAP, "test")[0];
            string directory = Path.Combine(Path.GetTempPath(), "locomotion-trace-test-" + Guid.NewGuid().ToString("N"));

            var frames = new List<LocomotionFrameSample>
            {
                new () { T = 0.5f, Kind = MovementKind.Idle, Jump = true, JumpTriggered = true, JumpCount = 1, Expected = tap.SampleAt(0.5f) },
                new () { T = 0.6f, Kind = MovementKind.Idle, Jump = true, JumpCount = 1, Expected = tap.SampleAt(0.6f) },
            };

            var run = new LocomotionRunInfo(1, false, "test", "2026-01-01T00:00:00Z", LocomotionInputSeam.Component, false, 0.02f, "ThirdPerson",
                Vector2Int.zero, Vector3.zero, 0f, doubleJump: true, gliding: false);

            try
            {
                LocomotionTraceSummary summary = LocomotionTraceWriter.Write(directory, tap, in run, frames);
                string[] lines = File.ReadAllLines(summary.Path);

                Assert.That(lines[0], Does.Contain("\"double_jump\":true,\"gliding\":false"));
                Assert.That(lines[1], Does.Contain("\"in\":{\"fwd\":0.0,\"right\":0.0,\"walk\":false,\"sprint\":false,\"jump\":true}"));
                Assert.That(lines[1], Does.Contain("\"jump_triggered\":true,\"jump_count\":1,\"glide\":\"closed\""));
                Assert.That(lines[2], Does.Contain("\"jump_triggered\":false"));
                Assert.That(summary.InputMismatchFrames, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}
