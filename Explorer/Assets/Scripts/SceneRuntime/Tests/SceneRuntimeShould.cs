using CrdtEcsBridge.Engine;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Factory;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace SceneRuntime.Tests
{
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

        private IInstancePoolsProvider poolsProvider;

        [SetUp]
        public void SetUp()
        {
            poolsProvider = Substitute.For<IInstancePoolsProvider>();
            poolsProvider.GetCrdtRawDataPool(Arg.Any<int>()).Returns(c => new byte[c.Arg<int>()]);
        }

        [UnityTest]
        public IEnumerator EngineApi_GetState() =>
            UniTask.ToCoroutine(async () =>
            {
                var engineApi = Substitute.For<IEngineApi>();

                var code = @"
            const engineApi = require('~system/EngineApi')
            exports.onStart = async function() {
                return engineApi.crdtGetState()
            };
            exports.onUpdate = async function(dt) {};
        ";

                SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCode(code, poolsProvider, CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi);
                await sceneRuntime.StartScene();

                engineApi.Received().CrdtGetState();
            });

        [UnityTest]
        public IEnumerator EngineApi_CrdtSendToRenderer() =>
            UniTask.ToCoroutine(async () =>
            {
                var engineApi = Substitute.For<IEngineApi>();

                var code = @"
            const engineApi = require('~system/EngineApi')
            exports.onStart = async function() {};
            exports.onUpdate = async function(dt) {
                data = new Uint8Array(10)
                data[0] = 123
                await engineApi.crdtSendToRenderer({ data })
                test.Ok()
            };
        ";

                SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCode(code, poolsProvider, CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi);

                var testOk = new TestUtilCheckOk();
                sceneRuntime.engine.AddHostObject("test", testOk);

                Assert.IsFalse(testOk.IsOk());

                await sceneRuntime.StartScene();

                // hot
                await sceneRuntime.UpdateScene(0.0f);

                for (int i = 0; i < 10; ++i)
                {
                    await UniTask.Yield();
                    await sceneRuntime.UpdateScene(0.01f);
                }

                Assert.IsTrue(testOk.IsOk());

                engineApi.Received().CrdtSendToRenderer(Arg.Is<ReadOnlyMemory<byte>>(r => CheckMemory(r)));
            });

        private static bool CheckMemory(ReadOnlyMemory<byte> r) =>
            r.Length == 10 && r.Span[0] == 123;

        [UnityTest]
        public IEnumerator ProfileOnUpdate() =>
            UniTask.ToCoroutine(async () =>
            {
                var code = @"
            exports.onStart = async function() {};
            exports.onUpdate = async function(dt) {};
        ";

                SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCode(code, poolsProvider, CancellationToken.None);

                await sceneRuntime.StartScene();

                // hot
                await sceneRuntime.UpdateScene(0.0f);

                for (int i = 0; i < 10; ++i)
                {
                    await UniTask.Yield();

                    Profiler.BeginSample("UpdateScene");
                    var ut = sceneRuntime.UpdateScene(0.01f);
                    Profiler.EndSample();

                    await ut;
                }
            });

        [UnityTest]
        public IEnumerator EngineApi_TestRealScene() =>
            UniTask.ToCoroutine(async () =>
            {
                var engineApi = Substitute.For<IEngineApi>();

                SceneRuntimeFactory sceneRuntimeFactory = new SceneRuntimeFactory();
                var path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateByPath(path, poolsProvider, CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi);

                await sceneRuntime.StartScene();

                // hot
                await UniTask.Yield();
                await sceneRuntime.UpdateScene(0.01f);

                for (int i = 0; i < 10; ++i)
                {
                    await UniTask.Yield();
                    await sceneRuntime.UpdateScene(0.01f);
                }
            });
    }
}
