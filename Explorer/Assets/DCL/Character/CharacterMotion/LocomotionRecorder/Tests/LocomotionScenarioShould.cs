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

        /// <summary>A free play converted to format 2: the camera track crosses 180 between its first two samples.</summary>
        private const string FREE_PLAY = @"{
            ""format"": 2,
            ""id"": ""free-play-replay"",
            ""fps"": 60,
            ""camera_yaw"": 170,
            ""settle"": 0.2,
            ""duration"": 300.0,
            ""events"": [
                { ""t"": 0.0, ""fwd"": 1 }
            ],
            ""camera"": [
                { ""t"": 0.0, ""yaw"": 170, ""pitch"": -20 },
                { ""t"": 1.0, ""yaw"": -170, ""pitch"": -10 },
                { ""t"": 2.0, ""yaw"": -100, ""pitch"": -10 }
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
        [TestCase("\"format\": 1", "\"format\": 3")]
        [TestCase("\"fps\": 60", "\"fps\": 0")]
        [TestCase("\"id\": \"jog-W-hold2.0-release\"", "\"id\": \"a/b\"")]
        public void RejectAnInvalidScenario(string valid, string invalid)
        {
            Assert.Throws<FormatException>(() => LocomotionScenario.Parse(JOG_W.Replace(valid, invalid), "test"));
        }

        [Test]
        public void ParseAFormat2CameraTrack()
        {
            LocomotionScenario scenario = LocomotionScenario.Parse(FREE_PLAY, "test")[0];

            Assert.That(scenario.HasCameraTrack, Is.True);
            Assert.That(scenario.CameraTrack.Count, Is.EqualTo(3));
            Assert.That(scenario.CameraTrack[1].T, Is.EqualTo(1f));
            Assert.That(scenario.CameraTrack[1].Yaw, Is.EqualTo(-170f));
            Assert.That(scenario.Duration, Is.EqualTo(300f));
        }

        [Test]
        public void ReadAFormat2ScenarioWithoutATrackAsAHeldYaw()
        {
            LocomotionScenario scenario = LocomotionScenario.Parse(JOG_W.Replace("\"format\": 1", "\"format\": 2"), "test")[0];

            Assert.That(scenario.HasCameraTrack, Is.False);
            Assert.That(scenario.CameraYawAt(3f), Is.EqualTo(90f));
        }

        [TestCase(-1f, 170f)]
        [TestCase(0f, 170f)]
        [TestCase(0.25f, 175f)]
        [TestCase(0.75f, -175f)]
        [TestCase(1f, -170f)]
        [TestCase(1.5f, -135f)]
        [TestCase(2f, -100f)]
        [TestCase(10f, -100f)]
        public void FollowTheCameraTrackTheShortWayRound(float t, float expectedYaw)
        {
            LocomotionScenario scenario = LocomotionScenario.Parse(FREE_PLAY, "test")[0];

            Assert.That(scenario.CameraYawAt(t), Is.EqualTo(expectedYaw).Within(1e-3f));
        }

        [TestCase("\"format\": 2", "\"format\": 1")]
        [TestCase("\"t\": 2.0, \"yaw\": -100", "\"t\": 0.5, \"yaw\": -100")]
        [TestCase("\"yaw\": -100", "\"yaw\": \"west\"")]
        [TestCase("\"yaw\": -170, \"pitch\": -10", "\"yaw\": -170")]
        [TestCase("\"pitch\": -20", "\"pitch\": -95")]
        [TestCase("\"camera\": [", "\"camera\": 1, \"unused\": [")]
        public void RejectAnInvalidCameraTrack(string valid, string invalid)
        {
            Assert.That(FREE_PLAY, Does.Contain(valid));
            Assert.Throws<FormatException>(() => LocomotionScenario.Parse(FREE_PLAY.Replace(valid, invalid), "test"));
        }

        [Test]
        public void WriteTheTrackYawInEachFrameOfATrackedRun()
        {
            LocomotionScenario tracked = LocomotionScenario.Parse(FREE_PLAY, "test")[0];
            LocomotionScenario held = LocomotionScenario.Parse(JOG_W, "test")[0];
            string directory = Path.Combine(Path.GetTempPath(), "locomotion-trace-test-" + Guid.NewGuid().ToString("N"));

            var frames = new List<LocomotionFrameSample> { new () { T = 0.25f, Kind = MovementKind.Jog, Fwd = 1f, CamYaw = tracked.CameraYawAt(0.25f), Expected = tracked.SampleAt(0.25f) } };

            var run = new LocomotionRunInfo(1, false, "test", "2026-01-01T00:00:00Z", LocomotionInputSeam.Component, false, 0.02f, "ThirdPerson",
                Vector2Int.zero, Vector3.zero, 0f, doubleJump: true, gliding: false);

            try
            {
                string[] trackedLines = File.ReadAllLines(LocomotionTraceWriter.Write(directory, tracked, in run, frames).Path);
                string[] heldLines = File.ReadAllLines(LocomotionTraceWriter.Write(directory, held, in run, frames).Path);

                Assert.That(trackedLines[0], Does.Contain("\"camera_control\":\"track\""));
                Assert.That(trackedLines[1], Does.Contain("\"cam\":{\"yaw\":175.0}"));
                Assert.That(heldLines[0], Does.Not.Contain("camera_control"));
                Assert.That(heldLines[1], Does.Not.Contain("\"cam\""));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
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

        [Test]
        public void WriteTheAnimatorStateOnlyOnRunsWithAnimations()
        {
            LocomotionScenario jog = LocomotionScenario.Parse(JOG_W, "test")[0];
            string directory = Path.Combine(Path.GetTempPath(), "locomotion-trace-test-" + Guid.NewGuid().ToString("N"));

            var walkToJogStop = new LocomotionAnimationSample
            {
                Present = true,
                Clips = new[]
                {
                    new LocomotionAnimClip { Clip = "Walk", W = 0.75f, T = 0.25f, Speed = 1f },
                    new LocomotionAnimClip { Clip = "Jog", W = 0.25f, T = 0.123456f, Speed = 1f },
                },
                State = Animator.StringToHash("Movement"),
                StateTime = 3.25f,
                Next = Animator.StringToHash("Jog_Stop"),
                NextTime = 0.2f,
                Progress = 0.25f,
                CurrentClipWeight = 1f,
                NextClipWeight = 0f,
                MovementBlend = 1.5f,
                IsGrounded = true,
                PointLayerWeight = 0f,
                RotationLayerWeight = float.NaN,
            };

            var frames = new List<LocomotionFrameSample>
            {
                new () { Kind = MovementKind.Jog, Fwd = 1f, Anim = walkToJogStop, Expected = jog.SampleAt(0f) },
                new () { Kind = MovementKind.Jog, Fwd = 1f, Expected = jog.SampleAt(0f) },
            };

            LocomotionRunInfo Run(bool animations) =>
                new (1, animations, "test", "2026-01-01T00:00:00Z", LocomotionInputSeam.Component, false, 0.02f, "ThirdPerson",
                    Vector2Int.zero, Vector3.zero, 0f, doubleJump: false, gliding: false);

            try
            {
                LocomotionRunInfo animated = Run(true);
                string[] lines = File.ReadAllLines(LocomotionTraceWriter.Write(directory, jog, in animated, frames).Path);

                Assert.That(lines[0], Does.Contain("\"anim_source\":\"capture"));
                Assert.That(lines[1], Does.Contain("\"anim\":[{\"clip\":\"Walk\",\"w\":0.75,\"t\":0.25,\"speed\":1.0},{\"clip\":\"Jog\",\"w\":0.25,\"t\":0.1235,\"speed\":1.0}]"));
                Assert.That(lines[1], Does.Contain("\"anim_params\":{\"MovementBlend\":1.5,\"SlideBlend\":0.0,\"JumpCount\":0,\"IsGrounded\":true,\"IsFalling\":false,\"IsLongFall\":false,\"IsStunned\":false,\"GlideBlend\":0.0}"));
                Assert.That(lines[1], Does.Contain("\"anim_state\":{\"state\":\"Movement\",\"next\":\"Jog_Stop\",\"progress\":0.25,\"nt\":3.25,\"next_nt\":0.2,\"cur_w\":1.0,\"next_w\":0.0}"));
                Assert.That(lines[1], Does.Contain("\"anim_layers\":{\"point\":0.0,\"rotation\":null}"));
                Assert.That(lines[2], Does.Contain("\"anim\":null}"));

                LocomotionRunInfo still = Run(false);
                string[] stillLines = File.ReadAllLines(LocomotionTraceWriter.Write(directory, jog, in still, frames).Path);

                Assert.That(stillLines[0], Does.Not.Contain("anim_source"));
                Assert.That(stillLines[1], Does.Not.Contain("\"anim"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void NameEveryControllerStateByItsHash()
        {
            var hashes = new HashSet<int>();

            foreach (string name in LocomotionAnimationSample.STATE_NAMES)
            {
                Assert.That(hashes.Add(Animator.StringToHash(name)), Is.True, name);
                Assert.That(LocomotionAnimationSample.StateName(Animator.StringToHash(name)), Is.EqualTo(name));
            }

            Assert.That(LocomotionAnimationSample.StateName(12345), Is.EqualTo("#12345"));
        }

        [Test]
        public void MergeAClipThatPlaysInBothStatesOfATransition()
        {
            var clips = new List<LocomotionAnimClip>();

            LocomotionAnimationSample.AddClip(clips, new LocomotionAnimClip { Clip = "Idle", W = 0.3f, T = 0.1f, Speed = 1f });
            LocomotionAnimationSample.AddClip(clips, new LocomotionAnimClip { Clip = "Walk", W = 0.2f, T = 0.5f, Speed = 1f });
            LocomotionAnimationSample.AddClip(clips, new LocomotionAnimClip { Clip = "Idle", W = 0.5f, T = 0.7f, Speed = 1.2f });

            Assert.That(clips.Count, Is.EqualTo(2));
            Assert.That(clips[0].W, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(clips[0].T, Is.EqualTo(0.7f));
            Assert.That(clips[0].Speed, Is.EqualTo(1.2f));
        }
    }
}
