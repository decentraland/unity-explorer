using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using ECS.TestSuite;
using DCL.Diagnostics;
using ECS;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
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
            poolsProvider.GetCrdtRawDataPool(Arg.Any<int>()).Returns(c => new PoolableByteArray(new byte[c.Arg<int>()], c.Arg<int>(), null));
        }

        private static SceneRuntimeFactory NewSceneRuntimeFactory() =>
            new (TestWebRequestController.INSTANCE, new IRealmData.Fake());

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

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                // sceneRuntime.RegisterEngineApi(engineApi, Substitute.For<ICommunicationsControllerAPI>(), sceneExceptionsHandler);
                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);
                sceneRuntime.ExecuteSceneJson();
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

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);
                // sceneRuntime.RegisterEngineApi(engineApi, Substitute.For<ICommunicationsControllerAPI>(), sceneExceptionsHandler);
                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);
                sceneRuntime.ExecuteSceneJson();

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

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);
                sceneRuntime.ExecuteSceneJson();
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

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                var path = URLAddress.FromString($"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}");
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(path, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                // sceneRuntime.RegisterEngineApi(engineApi, Substitute.For<ICommunicationsControllerAPI>(), sceneExceptionsHandler);
                sceneRuntime.RegisterEngineApi(engineApi, sceneExceptionsHandler);
                sceneRuntime.ExecuteSceneJson();

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
