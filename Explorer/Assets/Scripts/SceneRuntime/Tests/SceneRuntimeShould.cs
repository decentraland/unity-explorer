using System.Collections;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SceneRuntimeShould
{
    public class TestUtilCheckOk
    {
        private bool value;

        [UsedImplicitly]
        public void Ok()
        {
            value = true;
        }

        public bool IsOk()
        {
            return value;
        }
    }
    [UnityTest]
    public IEnumerator EngineApi_GetState() => UniTask.ToCoroutine(async () =>
    {
        var engineApi = Substitute.For<IEngineApi>();

        var code = @"
            const engineApi = require('~system/EngineApi')
            exports.onStart = async function() {
                await engineApi.crdtGetState()
            };
            exports.onUpdate = async function(dt) {};
        ";

        SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
        var sceneRuntime = await sceneRuntimeFactory.CreateBySourceCode(code);

        sceneRuntime.RegisterEngineApi(engineApi);
        await sceneRuntime.StartScene();

        await engineApi.Received().CrdtGetState();
    });

    [UnityTest]
    public IEnumerator EngineApi_CrdtSendToRenderer() => UniTask.ToCoroutine(async () =>
    {
        var engineApi = Substitute.For<IEngineApi>();

        var code = @"
            const engineApi = require('~system/EngineApi')
            exports.onStart = async function() {
                data = new Uint8Array(10)
                data[0] = 123
                await engineApi.crdtSendToRenderer({ data })
                test.Ok()
            };
            exports.onUpdate = async function(dt) {};
        ";

        SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
        var sceneRuntime = await sceneRuntimeFactory.CreateBySourceCode(code);

        sceneRuntime.RegisterEngineApi(engineApi);

        var testOk = new TestUtilCheckOk();
        sceneRuntime.engine.AddHostObject("test", testOk);

        Assert.IsFalse(testOk.IsOk());

        // hot
        await sceneRuntime.StartScene();

        // already hot
        for (int i = 0; i < 10; ++i)
        {
            await UniTask.Yield();
            await sceneRuntime.StartScene();
        }

        Assert.IsTrue(testOk.IsOk());

        await engineApi.Received().CrdtSendToRenderer(Arg.Is<ITypedArray<byte>>(array => array.Length == 10 && array.GetBytes()[0] == 123 ));
    });

    [UnityTest]
    public IEnumerator EngineApi_TestRealScene() => UniTask.ToCoroutine(async () =>
    {
        var engineApi = Substitute.For<IEngineApi>();

        SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
        var sceneRuntime = await sceneRuntimeFactory.CreateByPath("Scenes/Cube/cube.js");

        sceneRuntime.RegisterEngineApi(engineApi);

        await sceneRuntime.StartScene();

        // hot
        await UniTask.Yield();
        await sceneRuntime.UpdateScene(0.01f);

        // already hot
        for (int i = 0; i < 10; ++i)
        {
            await UniTask.Yield();
            await sceneRuntime.UpdateScene(0.01f);
        }
    });
}
