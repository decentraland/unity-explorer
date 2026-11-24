using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.Profiling;
using ECS.TestSuite;
using DCL.Diagnostics;
using ECS;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using Profiler = UnityEngine.Profiling.Profiler;

namespace SceneRuntime.Tests
{
    public class SceneRuntimeShould
    {
        private IInstancePoolsProvider poolsProvider = null!;
        private ISceneExceptionsHandler sceneExceptionsHandler = null!;

        [SetUp]
        public void SetUp()
        {
            sceneExceptionsHandler = new RethrowSceneExceptionsHandler();
            poolsProvider = Substitute.For<IInstancePoolsProvider>();
            poolsProvider.GetAPIRawDataPool(Arg.Any<int>()).Returns(c => new PoolableByteArray(new byte[c.Arg<int>()], c.Arg<int>(), null));
        }

        private static SceneRuntimeFactory NewSceneRuntimeFactory()
        {
            return new SceneRuntimeFactory(new IRealmData.Fake(), new V8EngineFactory(),
                new WebJsSources(new JsCodeResolver(TestWebRequestController.INSTANCE)));
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

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.RegisterEngineAPI(Substitute.For<ISceneData>(), engineApi, Substitute.For<IInstancePoolsProvider>(), sceneExceptionsHandler, new SceneRuntimeMetrics());
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
                sceneRuntime.RegisterEngineAPI(Substitute.For<ISceneData>(), engineApi, poolsProvider, sceneExceptionsHandler, new SceneRuntimeMetrics());
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

                sceneRuntime.RegisterEngineAPI(Substitute.For<ISceneData>(), engineApi, poolsProvider, sceneExceptionsHandler, new SceneRuntimeMetrics());
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

        // Regression (Sentry UNITY-EXPLORER-NQG): a racing double dispose cancelled an already-disposed CTS
        [UnityTest]
        public IEnumerator NotThrowOnSecondSetIsDisposing() =>
            UniTask.ToCoroutine(async () =>
            {
                var code = @"
            exports.onStart = async function() {};
            exports.onUpdate = async function(dt) {};
        ";

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);

                sceneRuntime.SetIsDisposing();

                Assert.DoesNotThrow(() => sceneRuntime.SetIsDisposing());
            });

        // A scene that imports ~system/SceneRewards must load in the explorer, which never pays: the call resolves a refusal.
        [UnityTest]
        public IEnumerator SceneRewards_ResolveNotAvailable() =>
            UniTask.ToCoroutine(async () =>
            {
                var code = @"
            const sceneRewards = require('~system/SceneRewards')
            exports.onStart = async function() {
                const response = await sceneRewards.grantReward({ programId: '', recipient: '', outcomeId: 'match-1' })
                probe.Record(sceneRewards.GrantRewardError.GRE_NOT_AVAILABLE, response.ok, response.error)
            };
            exports.onUpdate = async function(dt) {};
        ";

                var sceneRuntimeFactory = NewSceneRuntimeFactory();
                SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateBySourceCodeAsync(code, poolsProvider, new SceneShortInfo(), CancellationToken.None);
                sceneRuntime.ExecuteSceneJson();

                var probe = new GrantRewardProbe();
                sceneRuntime.engine.AddHostObject("probe", probe);

                await sceneRuntime.StartScene();

                for (var i = 0; i < 10 && !probe.Recorded; ++i)
                {
                    await UniTask.Yield();
                    await sceneRuntime.UpdateScene(0.01f);
                }

                Assert.IsTrue(probe.Recorded, "grantReward did not resolve");
                Assert.AreEqual(1d, probe.NotAvailable, "GrantRewardError.GRE_NOT_AVAILABLE");
                Assert.IsFalse(probe.Ok, "response.ok");
                Assert.AreEqual(probe.NotAvailable, probe.Error, "response.error");
            });

        public class GrantRewardProbe
        {
            public bool Recorded { get; private set; }
            public double NotAvailable { get; private set; }
            public bool Ok { get; private set; }
            public double Error { get; private set; }

            [UsedImplicitly]
            public void Record(double notAvailable, bool ok, double error)
            {
                Recorded = true;
                NotAvailable = notAvailable;
                Ok = ok;
                Error = error;
            }
        }

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
