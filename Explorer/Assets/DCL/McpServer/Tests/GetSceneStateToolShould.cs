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
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetSceneStateToolShould
    {
        private IScenesCache scenesCache = null!;
        private ICurrentSceneInfo currentSceneInfo = null!;
        private ILoadingStatus loadingStatus = null!;
        private GetSceneStateTool tool = null!;

        [SetUp]
        public void Setup()
        {
            scenesCache = Substitute.For<IScenesCache>();
            currentSceneInfo = Substitute.For<ICurrentSceneInfo>();
            loadingStatus = Substitute.For<ILoadingStatus>();

            scenesCache.CurrentParcel.Returns(new ReactiveProperty<Vector2Int>(new Vector2Int(1, 2)));
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));
            loadingStatus.CurrentStage.Returns(new ReactiveProperty<LoadingStatus.LoadingStage>(default));

            tool = new GetSceneStateTool(scenesCache, currentSceneInfo, loadingStatus, localSceneDevelopment: false);
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
            JObject schema = tool.OutputSchema!;

            // Assert
            Assert.That(schema["type"]!.Value<string>(), Is.EqualTo("object"));

            var sceneType = (JArray)schema["properties"]!["scene"]!["type"]!;
            Assert.That(sceneType.ToObject<string[]>(), Is.EqualTo(new[] { "object", "null" }));
        }

        private McpToolResult Execute() =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
