using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using ECS.TestSuite;
using DCL.Diagnostics;
using ECS;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Factory.WebSceneSource;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneRuntime.Factory.Tests
{
    public class SceneRuntimeFactoryShould
    {
        private readonly ISceneExceptionsHandler sceneExceptionsHandler = new RethrowSceneExceptionsHandler();

        private V8EngineFactory engineFactory;
        private IWebJsSources webJsSources;

        [SetUp]
        public void SetUp()
        {
            engineFactory = new V8EngineFactory();
            webJsSources = new WebJsSources(new JsCodeResolver(TestWebRequestController.INSTANCE));
        }

        [UnityTest]
        public IEnumerator CreateBySourceCode() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var factory = new SceneRuntimeFactory(new IRealmData.Fake(), engineFactory,
                    webJsSources);

                var sourceCode = @"
                const engineApi = require('~system/EngineApi')
                exports.onStart = async function() {
                    data = new Uint8Array(10)
                    data[0] = 123
                    await engineApi.crdtSendToRenderer({ data })
                    test.Ok()
                };
                exports.onUpdate = async function(dt) {};
            ";

                // Act
                IInstancePoolsProvider instancePoolsProvider = Substitute.For<IInstancePoolsProvider>();
                instancePoolsProvider.GetAPIRawDataPool(Arg.Any<int>()).Returns(c => new PoolableByteArray(new byte[c.Arg<int>()], c.Arg<int>(), null));

                using SceneRuntimeImpl sceneRuntime = await factory.CreateBySourceCodeAsync(sourceCode,
                    instancePoolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.ExecuteSceneJson();

                // Assert
                Assert.NotNull(sceneRuntime);

                //Assert: Run an update
                await sceneRuntime.UpdateScene(0.01f);
            });

        [UnityTest]
        public IEnumerator CreateByPath() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var factory = new SceneRuntimeFactory(new IRealmData.Fake(), engineFactory,
                    webJsSources);

                var path = URLAddress.FromString($"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}");

                // Act
                IEngineApi engineApi = Substitute.For<IEngineApi>();
                IInstancePoolsProvider instancePoolsProvider = Substitute.For<IInstancePoolsProvider>();

                instancePoolsProvider.GetAPIRawDataPool(Arg.Any<int>())
                                     .Returns(c => new PoolableByteArray(new byte[c.Arg<int>()], c.Arg<int>(), _ => { }));

                using SceneRuntimeImpl sceneRuntime = await factory.CreateByPathAsync(path,
                    instancePoolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.RegisterEngineAPI(engineApi, instancePoolsProvider, sceneExceptionsHandler);
                sceneRuntime.ExecuteSceneJson();

                // Assert
                Assert.NotNull(sceneRuntime);

                await UniTask.Yield();
                await sceneRuntime.UpdateScene(0.01f);
            });

        [Test]
        public void WrapInModuleCommonJs()
        {
            // Arrange
            var factory = new SceneRuntimeFactory(new IRealmData.Fake(), engineFactory, webJsSources);
            var sourceCode = "console.log('Hello, world!');";

            // Act
            string moduleWrapper = factory.WrapInModuleCommonJs(sourceCode);

            // Assert: Check that the module compiles
            using var engine = engineFactory.Create(new SceneShortInfo());
            engine.Compile(moduleWrapper);
        }
    }
}
