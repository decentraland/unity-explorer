using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

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
            SceneRuntime sceneRuntime = await factory.CreateBySourceCode(sourceCode);

            // Assert
            Assert.NotNull(sceneRuntime);
            Assert.IsInstanceOf<SceneRuntime>(sceneRuntime);

            //Assert: Run an update
            await sceneRuntime.UpdateScene(0.01f);
        });

    [UnityTest]
    public IEnumerator CreateByStreamingAssetsPath() =>
        UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var factory = new SceneRuntimeFactory();
            var path = $"{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";


            // Act
            var engineApi = Substitute.For<IEngineApi>();
            SceneRuntime sceneRuntime = await factory.CreateByPath(path);
            sceneRuntime.RegisterEngineApi(engineApi);

            // Assert
            Assert.NotNull(sceneRuntime);
            Assert.IsInstanceOf<SceneRuntime>(sceneRuntime);

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
