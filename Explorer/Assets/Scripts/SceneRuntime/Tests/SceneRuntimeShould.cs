using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using ECS.TestSuite;
using DCL.Diagnostics;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.EngineApi;
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
        private IInstancePoolsProvider poolsProvider;
        private ISceneExceptionsHandler sceneExceptionsHandler;

        [SetUp]
        public void SetUp()
        {
            sceneExceptionsHandler = new RethrowSceneExceptionsHandler();
            poolsProvider = Substitute.For<IInstancePoolsProvider>();
            poolsProvider.GetCrdtRawDataPool(Arg.Any<int>()).Returns(c => new byte[c.Arg<int>()]);
        }

        [UnityTest]
        public IEnumerator EngineApi_GetState() =>
            UniTask.ToCoroutine(async () =>
            {
                IEngineApi engineApi = Substitute.For<IEngineApi>();

                var code = @"
            const engineApi = require('~system/EngineApi')
            exports.onStart = async function() {
                return engineApi.crdtGetState()
            };
            exports.onUpdate = async function(dt) {};
        ";

                var sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);
                await sceneRuntime.StartScene();

                engineApi.Received().CrdtGetState();
            });

        [UnityTest]
        public IEnumerator EngineApi_CrdtSendToRenderer() =>
            UniTask.ToCoroutine(async () =>
            {
                IEngineApi engineApi = Substitute.For<IEngineApi>();

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

                var sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);

                var testOk = new TestUtilCheckOk();
                sceneRuntime.engine.AddHostObject("test", testOk);

                Assert.IsFalse(testOk.IsOk());

                await sceneRuntime.StartScene();

                // hot
                await sceneRuntime.UpdateScene(0.0f);

                for (var i = 0; i < 10; ++i)
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

                var sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                await sceneRuntime.StartScene();

                // hot
                await sceneRuntime.UpdateScene(0.0f);

                for (var i = 0; i < 10; ++i)
                {
                    await UniTask.Yield();

                    Profiler.BeginSample("UpdateScene");
                    UniTask ut = sceneRuntime.UpdateScene(0.01f);
                    Profiler.EndSample();

                    await ut;
                }
            });

        [UnityTest]
        public IEnumerator EngineApi_TestRealScene() =>
            UniTask.ToCoroutine(async () =>
            {
                IEngineApi engineApi = Substitute.For<IEngineApi>();

                var sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);
                var path = URLAddress.FromString($"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}");
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(path, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);

                await sceneRuntime.StartScene();

                // hot
                await UniTask.Yield();
                await sceneRuntime.UpdateScene(0.01f);

                for (var i = 0; i < 10; ++i)
                {
                    await UniTask.Yield();
                    await sceneRuntime.UpdateScene(0.01f);
                }
            });

        public class TestUtilCheckOk
        {
            private bool value;

            [UsedImplicitly]
            public void Ok()
            {
                value = true;
            }

            public bool IsOk() =>
                value;
        }
    }
}
