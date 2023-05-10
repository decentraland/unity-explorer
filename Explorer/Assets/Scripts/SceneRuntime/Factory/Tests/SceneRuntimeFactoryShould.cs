using CrdtEcsBridge.Engine;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using SceneRuntime.Apis.Modules;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneRuntime.Factory.Tests
{
    public class SceneRuntimeFactoryShould
    {
        [UnityTest]
        public IEnumerator CreateBySourceCode() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var factory = new SceneRuntimeFactory();

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
                instancePoolsProvider.GetCrdtRawDataPool(Arg.Any<int>()).Returns(c => new byte[c.Arg<int>()]);

                SceneRuntimeImpl sceneRuntime = await factory.CreateBySourceCode(sourceCode, instancePoolsProvider, CancellationToken.None);

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
                var factory = new SceneRuntimeFactory();
                var path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

                // Act
                var engineApi = Substitute.For<IEngineApi>();
                IInstancePoolsProvider instancePoolsProvider = Substitute.For<IInstancePoolsProvider>();
                instancePoolsProvider.GetCrdtRawDataPool(Arg.Any<int>()).Returns(c => new byte[c.Arg<int>()]);

                SceneRuntimeImpl sceneRuntime = await factory.CreateByPath(path, instancePoolsProvider, CancellationToken.None);
                sceneRuntime.RegisterEngineApi(engineApi);

                // Assert
                Assert.NotNull(sceneRuntime);

                await UniTask.Yield();
                await sceneRuntime.UpdateScene(0.01f);
            });

        [Test]
        public void WrapInModuleCommonJs()
        {
            // Arrange
            var factory = new SceneRuntimeFactory();
            var sourceCode = "console.log('Hello, world!');";

            // Act
            string moduleWrapper = factory.WrapInModuleCommonJs(sourceCode);

            // Assert: Check that the module compiles
            V8EngineFactory.Create().Compile(moduleWrapper);
        }
    }
}
