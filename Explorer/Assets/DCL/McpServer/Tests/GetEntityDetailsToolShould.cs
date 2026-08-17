using CRDT.Attribution;
using CRDT.Protocol;
using DCL.Ipfs;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class GetEntityDetailsToolShould
    {
        private const string CURRENT_SCENE = "CURRENT";
        private const string SCENE_ID = "scene-abc";
        private const int CRDT_ENTITY = 512;

        private IWorldInfoHub worldInfoHub = null!;
        private IWorldInfo worldInfo = null!;
        private IScenesCache scenesCache = null!;
        private ICrdtWriterLog writerLog = null!;
        private GetEntityDetailsTool tool = null!;

        [SetUp]
        public void Setup()
        {
            worldInfo = Substitute.For<IWorldInfo>();
            worldInfoHub = Substitute.For<IWorldInfoHub>();
            worldInfoHub.WorldInfo(CURRENT_SCENE).Returns(worldInfo);
            worldInfo.TryGetCrdtEntityId(5, out Arg.Any<int>())
                     .Returns(call =>
                      {
                          call[1] = CRDT_ENTITY;
                          return true;
                      });

            // Built before the Returns() call, never inside its argument: configuring one substitute while another
            // one's call is pending loses NSubstitute's record of which call it was meant to configure.
            ISceneFacade scene = SceneWithId(SCENE_ID);

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));

            writerLog = Substitute.For<ICrdtWriterLog>();

            tool = new GetEntityDetailsTool(worldInfoHub, scenesCache, writerLog);
        }

        [Test]
        public void ReturnTheDumpWholeWhenItFitsTheBudget()
        {
            // Arrange
            const string DUMP = "Components of entity 5, total count: 1\n1) PBTransform";
            worldInfo.EntityComponentsInfo(5).Returns(DUMP);

            // Act
            string text = TextOf(Execute(5));

            // Assert
            Assert.That(text, Does.StartWith(DUMP));
            Assert.That(text, Does.Not.Contain("truncated"));
        }

        [Test]
        public void TruncateWithANoteWhenTheDumpExceedsTheBudget()
        {
            // Arrange
            string dump = new ('x', 20000);
            worldInfo.EntityComponentsInfo(5).Returns(dump);

            // Act
            string text = TextOf(Execute(5));

            // Assert
            Assert.That(text.Length, Is.LessThan(dump.Length));
            Assert.That(text, Does.Contain($"output truncated at 8000/{dump.Length} chars"));
        }

        [Test]
        public void ErrorWhenNoSceneWorldIsFound()
        {
            // Arrange
            worldInfoHub.WorldInfo(CURRENT_SCENE).Returns((IWorldInfo?)null);

            // Act
            McpToolResult result = Execute(5);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ErrorWhenEntityIdIsMissing()
        {
            // Act
            McpToolResult result = tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        /// <summary>
        ///     The reading an authoritative game is after: the component the agent is looking at was last set by a
        ///     player, not by the server that is supposed to own it.
        /// </summary>
        [Test]
        public void NameTheAddressThatLastWroteEachComponent()
        {
            // Arrange
            worldInfo.EntityComponentsInfo(5).Returns("Components of entity 5, total count: 1\n1) PBTransform");

            writerLog.When(log => log.EntityWrites(SCENE_ID, CRDT_ENTITY, Arg.Any<List<CrdtWrite>>()))
                     .Do(call => call.Arg<List<CrdtWrite>>()
                                     .Add(new CrdtWrite(CRDT_ENTITY, 1, "0xabc", false, false, CRDTMessageType.PUT_COMPONENT_NETWORK, 9, 1.5)));

            // Act
            McpToolResult result = Execute(5);

            // Assert
            var writes = (JArray)result.Payload["structuredContent"]!["networkWrites"]!;
            Assert.That(writes.Count, Is.EqualTo(1));
            Assert.That(writes[0]!["writer"]!.Value<string>(), Is.EqualTo("0xabc"));
            Assert.That(writes[0]!["isAuthoritativeServer"]!.Value<bool>(), Is.False);
            Assert.That(writes[0]!["componentId"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(writes[0]!["crdtTimestamp"]!.Value<int>(), Is.EqualTo(9));

            // Assert — the text mirror carries the same claim, for clients that do not read structured content
            Assert.That(TextOf(result), Does.Contain("component 1 ← 0xabc"));
        }

        [Test]
        public void SayExplicitlyWhenNoPeerHasWrittenTheEntity()
        {
            // Arrange
            worldInfo.EntityComponentsInfo(5).Returns("Components of entity 5, total count: 1\n1) PBTransform");

            // Act
            McpToolResult result = Execute(5);

            // Assert
            Assert.That(((JArray)result.Payload["structuredContent"]!["networkWrites"]!).Count, Is.Zero);
            Assert.That(TextOf(result), Does.Contain("none — every component of this entity was written by the scene's own code"));
        }

        [Test]
        public void ReportANullCrdtEntityForAnEntityTheSceneNeverRegistered()
        {
            // Arrange
            worldInfo.EntityComponentsInfo(7).Returns("Entity not found: 7");
            worldInfo.TryGetCrdtEntityId(7, out Arg.Any<int>()).Returns(false);

            // Act
            McpToolResult result = Execute(7);

            // Assert
            Assert.That(result.Payload["structuredContent"]!["crdtEntityId"]!.Type, Is.EqualTo(JTokenType.Null));
            writerLog.DidNotReceive().EntityWrites(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<List<CrdtWrite>>());
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange
            worldInfo.EntityComponentsInfo(5).Returns("Components of entity 5, total count: 0");

            // Act
            var structured = (JObject)Execute(5).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        private static ISceneFacade SceneWithId(string sceneId)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = sceneId });

            ISceneFacade scene = Substitute.For<ISceneFacade>();
            scene.SceneData.Returns(sceneData);
            return scene;
        }

        private McpToolResult Execute(int entityId) =>
            tool.ExecuteAsync(new JObject { ["entityId"] = entityId }, CancellationToken.None).GetAwaiter().GetResult();

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;
    }
}
