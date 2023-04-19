using System.Collections;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;
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

        var sceneRuntime = new SceneRuntime(code);

        sceneRuntime.RegisterEngineApi(engineApi);

        await sceneRuntime.StartScene();

        engineApi.Received().CrdtGetState();
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

        var sceneRuntime = new SceneRuntime(code);

        sceneRuntime.RegisterEngineApi(engineApi);

        var testOk = new TestUtilCheckOk();
        sceneRuntime.engine.AddHostObject("test", testOk);
        
        Assert.IsFalse(testOk.IsOk());

        await sceneRuntime.StartScene();
        
        Assert.IsTrue(testOk.IsOk());
        
        engineApi.Received().CrdtSendToRenderer(Arg.Is<ITypedArray<byte>>(array => array.Length == 10 && array.GetBytes()[0] == 123 ));
    });
}
