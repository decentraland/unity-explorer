using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using DCL.SkyBox.Components;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections;
using System.Threading;
using Utility;

namespace DCL.McpServer.Tests
{
    public class ReloadSceneToolShould
    {
        private World world = null!;
        private Entity playerEntity;
        private Entity skyboxEntity;
        private IScenesCache scenesCache = null!;
        private ISceneFacade scene = null!;
        private ICoroutineRunner coroutineRunner = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();
            skyboxEntity = world.Create();

            scene = Substitute.For<ISceneFacade>();
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));

            // Runs each coroutine to completion on the spot, so the end-of-frame wait completes synchronously.
            coroutineRunner = Substitute.For<ICoroutineRunner>();
            coroutineRunner.StartCoroutine(Arg.Do<IEnumerator>(RunToCompletion));
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void ErrorWithoutACurrentScene()
        {
            // Arrange
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));
            ReloadSceneTool tool = CreateTool(_ => UniTask.FromResult<ISceneFacade?>(scene), Settled(2));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
        }

        [Test]
        public void ReportTicksAndRenderedFramesFromTheSettleWait()
        {
            // Arrange
            ReloadSceneTool tool = CreateTool(_ => UniTask.FromResult<ISceneFacade?>(scene), Settled(2));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["reloaded"]!.Value<bool>(), Is.True);
            Assert.That(structured["settled"]!.Value<bool>(), Is.True);
            Assert.That(structured["sceneState"]!.Value<string>(), Is.EqualTo("running"));
            Assert.That(structured["ticksWaited"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["renderedFrames"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(structured["warning"]!.Type, Is.EqualTo(JTokenType.Null));
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        [Test]
        public void WarnWhenTheSceneDidNotSettle()
        {
            // Arrange
            ReloadSceneTool tool = CreateTool(_ => UniTask.FromResult<ISceneFacade?>(scene),
                (_, _, _, _, _) => UniTask.FromResult(new SceneSettleWait.Result(false, SceneState.JavaScriptError, 0, 120)));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["settled"]!.Value<bool>(), Is.False);
            Assert.That(structured["sceneState"]!.Value<string>(), Is.EqualTo("java_script_error"));
            Assert.That(structured["warning"]!.Value<string>(), Does.Contain("java_script_error"));
        }

        [Test]
        public void ErrorWhenNothingWasReloadable()
        {
            // Arrange
            ReloadSceneTool tool = CreateTool(_ => UniTask.FromResult<ISceneFacade?>(null), Settled(2));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
        }

        [Test]
        public void ReleaseTheMotionAndSkyboxFreezeAfterTheReload()
        {
            // Arrange
            ReloadSceneTool tool = CreateTool(_ => UniTask.FromResult<ISceneFacade?>(scene), Settled(2));

            // Act
            Execute(tool);

            // Assert
            Assert.That(world.Has<StopCharacterMotion>(playerEntity), Is.False);
            Assert.That(world.Has<PauseSkyboxTimeUpdate>(skyboxEntity), Is.False);
        }

        private ReloadSceneTool CreateTool(ReloadSceneTool.Reload reload, SceneSettleWait.WaitForFreshScene settle) =>
            new (reload, scenesCache, world, playerEntity, skyboxEntity, coroutineRunner, settle);

        private static SceneSettleWait.WaitForFreshScene Settled(uint ticks) =>
            (_, _, _, _, _) => UniTask.FromResult(new SceneSettleWait.Result(true, SceneState.Running, ticks, 30));

        private static void RunToCompletion(IEnumerator coroutine)
        {
            while (coroutine.MoveNext()) { }
        }

        /// <summary>Every await in the tool completes synchronously with the scripted reload, settle and coroutine runner.</summary>
        private static McpToolResult Execute(ReloadSceneTool tool)
        {
            UniTask<McpToolResult> call = tool.ExecuteAsync(new JObject(), CancellationToken.None);
            Assert.That(call.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return call.GetAwaiter().GetResult();
        }
    }
}
