using CRDT.Attribution;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.McpServer.Tests
{
    public class GetSceneStateToolShould
    {
        private IScenesCache scenesCache = null!;
        private ICurrentSceneInfo currentSceneInfo = null!;
        private ILoadingStatus loadingStatus = null!;
        private ICrdtWriterLog writerLog = null!;
        private GetSceneStateTool tool = null!;

        [SetUp]
        public void Setup()
        {
            scenesCache = Substitute.For<IScenesCache>();
            currentSceneInfo = Substitute.For<ICurrentSceneInfo>();
            loadingStatus = Substitute.For<ILoadingStatus>();
            writerLog = Substitute.For<ICrdtWriterLog>();

            scenesCache.CurrentParcel.Returns(new ReactiveProperty<Vector2Int>(new Vector2Int(1, 2)));
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));
            loadingStatus.CurrentStage.Returns(new ReactiveProperty<LoadingStatus.LoadingStage>(default));

            tool = new GetSceneStateTool(scenesCache, currentSceneInfo, loadingStatus, writerLog, localSceneDevelopment: false);
        }

        [Test]
        public void MirrorTheStateInBothTextAndStructuredContent()
        {
            // Act
            McpToolResult result = Execute();

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured, Is.Not.Null);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Is.EqualTo(structured.ToString(Formatting.Indented)));
        }

        [Test]
        public void ReportAnAbsentSceneAsAJsonNull()
        {
            // Act
            McpToolResult result = Execute();

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["scene"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void DeclareAnObjectOutputSchemaThatAdmitsANullScene()
        {
            // Act
            JObject schema = tool.OutputSchema;

            // Assert
            Assert.That(schema["type"]!.Value<string>(), Is.EqualTo("object"));

            var sceneType = (JArray)schema["properties"]!["scene"]!["type"]!;
            Assert.That(sceneType.ToObject<string[]>(), Is.EqualTo(new[] { "object", "null" }));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange — a populated scene so the nested "scene" object is covered, not just the top level.
            ArrangeScene(authoritativeMultiplayer: false);

            // Act
            var structured = (JObject)Execute().Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);

            // Assert — the definition id is what click_entity's sceneId pin expects
            Assert.That(structured["scene"]!["sceneId"]!.Value<string>(), Is.EqualTo("scene-abc"));
        }

        [Test]
        public void NameTheAddressesThatWroteTheSceneState()
        {
            // Arrange — an authoritative scene the server and one player have both written to
            ArrangeScene(authoritativeMultiplayer: true);

            writerLog.When(log => log.SceneWriters("scene-abc", Arg.Any<List<CrdtWriterSummary>>()))
                     .Do(call =>
                      {
                          var writers = call.Arg<List<CrdtWriterSummary>>();
                          writers.Add(new CrdtWriterSummary(ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS, true, 12, 0, 4.5));
                          writers.Add(new CrdtWriterSummary("0xabc", false, 1, 0, 0.25));
                      });

            // Act
            var scene = (JObject)Execute().Payload["structuredContent"]!["scene"]!;

            // Assert
            Assert.That(scene["authoritativeMultiplayer"]!.Value<bool>(), Is.True);

            var writers = (JArray)scene["networkWriters"]!;
            Assert.That(writers.Count, Is.EqualTo(2));

            // Assert — most recent first, so the peer that just wrote to an authoritative scene reads first
            Assert.That(writers[0]!["address"]!.Value<string>(), Is.EqualTo("0xabc"));
            Assert.That(writers[0]!["isAuthoritativeServer"]!.Value<bool>(), Is.False);
            Assert.That(writers[1]!["address"]!.Value<string>(), Is.EqualTo(ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS));
            Assert.That(writers[1]!["isAuthoritativeServer"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ReportNoWritersForASceneNobodyHasWrittenTo()
        {
            // Arrange
            ArrangeScene(authoritativeMultiplayer: false);

            // Act
            var scene = (JObject)Execute().Payload["structuredContent"]!["scene"]!;

            // Assert
            Assert.That(scene["authoritativeMultiplayer"]!.Value<bool>(), Is.False);
            Assert.That(((JArray)scene["networkWriters"]!).Count, Is.Zero);
            Assert.That(scene["droppedWriteRecords"]!.Value<int>(), Is.Zero);
        }

        private void ArrangeScene(bool authoritativeMultiplayer)
        {
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);

            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition
            {
                id = "scene-abc",
                metadata = new SceneMetadata { authoritativeMultiplayer = authoritativeMultiplayer },
            });

            ISceneFacade scene = Substitute.For<ISceneFacade>();
            scene.Info.Returns(new SceneShortInfo(new Vector2Int(1, 2), "Test scene", "7"));
            scene.SceneStateProvider.Returns(sceneStateProvider);
            scene.SceneData.Returns(sceneData);
            scene.IsSceneReady().Returns(true);

            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));
            currentSceneInfo.SceneStatus.Returns(new ReactiveProperty<ICurrentSceneInfo.RunningStatus?>(null));
        }

        private McpToolResult Execute() =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
