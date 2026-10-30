using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class MoveToToolShould
    {
        private static readonly Vector3 TARGET = new (2388f, 0f, 2385f);
        private static readonly Vector3 LOOK_AT = new (2388f, 1f, 2401f);

        private World world = null!;
        private Entity playerEntity;
        private GameObject playerGameObject = null!;
        private CancellationTokenSource cts = null!;
        private IGlobalWorldActions globalWorldActions = null!;
        private MoveToTool tool = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();
            playerGameObject = new GameObject(nameof(MoveToToolShould));
            cts = new CancellationTokenSource();

            playerEntity = world.Create(new CharacterTransform(playerGameObject.transform));
            world.Create(new CameraComponent()); // the tool waits on the camera entity's look-at intent

            globalWorldActions = Substitute.For<IGlobalWorldActions>();

            globalWorldActions.MoveAndRotatePlayerAsync(Arg.Any<Vector3>(), Arg.Any<Vector3?>(), Arg.Any<Vector3?>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
                              .Returns(UniTask.FromResult(true));

            tool = new MoveToTool(globalWorldActions, world, playerEntity, new ExposedCameraData(), new SyntheticInputAgent(world, playerEntity));
        }

        [TearDown]
        public void TearDown()
        {
            // A started call is parked at its first frame delay or on the synthetic look-at. Cancel it there, so it never touches the disposed world.
            cts.Cancel();
            cts.Dispose();
            world.Dispose();
            Object.DestroyImmediate(playerGameObject);
        }

        [Test]
        public void RotateTheCameraTowardTheLookAtFromTheDestination()
        {
            // Both world actions are issued synchronously before the first await, in the SDK's movePlayerTo order.
            tool.ExecuteAsync(MoveArgs(withLookAt: true), cts.Token).Forget();

            Received.InOrder(() =>
            {
                globalWorldActions.RotateCamera(LOOK_AT, TARGET);
                globalWorldActions.MoveAndRotatePlayerAsync(TARGET, LOOK_AT, LOOK_AT, 0f, Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public void PostTheSyntheticLookAtOnThePlayerAfterTheMove()
        {
            // Act — the move completes synchronously and no production camera intent is pending, so the refinement is reached before the first frame delay.
            tool.ExecuteAsync(MoveArgs(withLookAt: true), cts.Token).Forget();

            // Assert
            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.True);
            Assert.That(world.Get<SyntheticCameraLookIntent>(playerEntity).LookAtTarget, Is.EqualTo(LOOK_AT));
        }

        [Test]
        public void NotPostASyntheticLookAtWithoutALookAt()
        {
            // Act
            tool.ExecuteAsync(MoveArgs(withLookAt: false), cts.Token).Forget();

            // Assert
            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False);
        }

        [Test]
        public void NotRotateTheCameraWithoutALookAt()
        {
            tool.ExecuteAsync(MoveArgs(withLookAt: false), cts.Token).Forget();

            globalWorldActions.DidNotReceive().RotateCamera(Arg.Any<Vector3?>(), Arg.Any<Vector3>());
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(TARGET, null, null, 0f, Arg.Any<CancellationToken>());
        }

        [Test]
        public void RefuseAPartialLookAt()
        {
            JObject arguments = MoveArgs(withLookAt: false);
            arguments["lookAtX"] = LOOK_AT.x;

            McpToolResult result = Execute(arguments);

            AssertError(result, "lookAtX, lookAtY and lookAtZ must be provided together");
            globalWorldActions.DidNotReceive().RotateCamera(Arg.Any<Vector3?>(), Arg.Any<Vector3>());
            globalWorldActions.DidNotReceive().MoveAndRotatePlayerAsync(Arg.Any<Vector3>(), Arg.Any<Vector3?>(), Arg.Any<Vector3?>(), Arg.Any<float>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void RefuseNonNumericCoordinates()
        {
            JObject arguments = MoveArgs(withLookAt: false);
            arguments["y"] = "0.0";

            McpToolResult result = Execute(arguments);

            AssertError(result, "x, y and z world coordinates are required");
            globalWorldActions.DidNotReceive().MoveAndRotatePlayerAsync(Arg.Any<Vector3>(), Arg.Any<Vector3?>(), Arg.Any<Vector3?>(), Arg.Any<float>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void DescribeItselfAsATeleportThatBypassesLocomotion()
        {
            Assert.That(tool.Description, Does.Contain("Teleport"));
            Assert.That(tool.Description, Does.Contain("walk"));
        }

        /// <summary>Only for calls that return before the tool's first frame delay (argument refusals).</summary>
        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, cts.Token).GetAwaiter().GetResult();

        private static void AssertError(McpToolResult result, string expectedText)
        {
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]?[0]?["text"]?.Value<string>(), Does.Contain(expectedText));
        }

        private static JObject MoveArgs(bool withLookAt)
        {
            var arguments = new JObject
            {
                ["x"] = TARGET.x,
                ["y"] = TARGET.y,
                ["z"] = TARGET.z,
            };

            if (!withLookAt)
                return arguments;

            arguments["lookAtX"] = LOOK_AT.x;
            arguments["lookAtY"] = LOOK_AT.y;
            arguments["lookAtZ"] = LOOK_AT.z;
            return arguments;
        }
    }
}
