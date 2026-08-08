using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class PressInputActionToolShould
    {
        private World world = null!;
        private Entity playerEntity;
        private PressInputActionTool tool = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();
            tool = new PressInputActionTool(world, playerEntity);
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            // Accepted calls stay awaiting a system this suite does not run; cancelling unwinds them.
            cts.Cancel();
            cts.Dispose();
            world.Dispose();
        }

        [Test]
        public void OfferEveryInputActionOnTheWire()
        {
            // A scene may read any action, and the production key map binds all of them, so the tool narrows
            // nothing — unlike click_entity, which passes a subset of the same enum.

            // Arrange
            var wireNames = new List<string>();

            foreach (JToken value in tool.InputSchema["properties"]!["action"]!["enum"]!)
                wireNames.Add(value.Value<string>()!);

            // Assert
            Assert.That(wireNames, Is.EqualTo(McpWireEnum<McpInputAction>.WIRE_NAMES));
        }

        [Test]
        public void SpellEveryProtobufInputActionMemberOnTheWire()
        {
            // McpInputAction converts to InputAction by cast, so it has to stay a faithful renaming: same member
            // count, and each member carrying the value of the protobuf member its own name spells. A member
            // added or renumbered upstream fails here instead of quietly sending a scene the wrong action.

            // Arrange
            var wireMembers = (McpInputAction[])Enum.GetValues(typeof(McpInputAction));

            // Assert
            Assert.That(wireMembers.Length, Is.EqualTo(Enum.GetValues(typeof(InputAction)).Length));

            foreach (McpInputAction wireMember in wireMembers)
            {
                // "ACTION_5" ↔ "IaAction5": the protobuf spelling of the same name.
                string protobufName = wireMember.ToInputAction().ToString().ToUpperInvariant();
                Assert.That(protobufName, Is.EqualTo($"IA{wireMember.ToString().Replace("_", string.Empty)}"),
                    $"{wireMember} maps to {wireMember.ToInputAction()}, which is a different action");
            }
        }

        [TestCase("{}", "action is required")]
        [TestCase("{'action':'ia_action_5'}", "action is required")]
        [TestCase("{'action':'action_5','eventType':'tap'}", "eventType must be one of")]
        public void RefuseArgumentsItCannotActOn(string arguments, string expectedFragment)
        {
            // Act: a refusal is answered before the tool's first await, so the task is already done.
            UniTask<McpToolResult> call = tool.ExecuteAsync(JObject.Parse(arguments), cts.Token);

            // Assert
            Assert.That(call.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the tool was expected to refuse");
            McpToolResult result = call.GetAwaiter().GetResult();
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain(expectedFragment));
            Assert.That(world.Has<McpInputActionIntent>(playerEntity), Is.False);
        }

        [TestCase("{'action':'action_5','eventType':'down'}", InputAction.IaAction5, PointerEventType.PetDown, null)]
        [TestCase("{'action':'primary','eventType':'up'}", InputAction.IaPrimary, PointerEventType.PetUp, null)]
        [TestCase("{'action':'action_5','holdSec':900}", InputAction.IaAction5, PointerEventType.PetDown, 30f)]
        public void InstallTheRequestedLegWithItsHoldClamped(string arguments, InputAction action, PointerEventType eventType, float? holdSeconds)
        {
            // Act: an accepted call is left awaiting the system this suite does not run, so the request it
            // installed on the player entity is what the test reads.
            tool.ExecuteAsync(JObject.Parse(arguments), cts.Token).Forget();

            // Assert
            McpInputActionIntent intent = world.Get<McpInputActionIntent>(playerEntity);
            Assert.That(intent.Action, Is.EqualTo(action));
            Assert.That(intent.EventType, Is.EqualTo(eventType));
            Assert.That(intent.HoldSeconds, Is.EqualTo(holdSeconds));
        }

        [Test]
        public void PinTheRequestToASceneWhenAsked()
        {
            // Act
            tool.ExecuteAsync(new JObject { ["action"] = "action_5", ["sceneId"] = "scene-here" }, cts.Token).Forget();

            // Assert
            Assert.That(world.Get<McpInputActionIntent>(playerEntity).SceneId, Is.EqualTo("scene-here"));
        }
    }
}
