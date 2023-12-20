using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.Web3Authentication;
using ECS.Prioritization.Components;
using Global;
using UnityEngine.TestTools;
using System.Collections;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources.Tests.PlayMode
{
    public class SDKAudioSourcesAutomatedTests
    {
        private const string TEST_SCENE = "Dance-floor";

        private readonly CancellationTokenSource tearDownCts = new ();

        private readonly PluginSettingsContainer globalPluginSettings =
            AssetDatabase.LoadAssetAtPath<PluginSettingsContainer>("Assets/DCL/PluginSystem/Global/Global Plugins Settings.asset");
        private readonly PluginSettingsContainer scenePluginSettings =
            AssetDatabase.LoadAssetAtPath<PluginSettingsContainer>("Assets/DCL/PluginSystem/World/World Plugins Container.asset");

        private StaticContainer? staticContainer;
        private ISceneFacade? currentScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return currentScene?.DisposeAsync().ToCoroutine();

            tearDownCts.Cancel();
            tearDownCts.Dispose();

            staticContainer?.Dispose();
        }

        [UnityTest]
        public IEnumerator VV()
        {
            InitializationFlowAsync(tearDownCts.Token).Forget();

            yield return new WaitForSeconds(1);
            Assert.IsTrue(true);
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                var web3Authenticator = new FakeWeb3Authenticator();
                await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettings, scenePluginSettings,
                    web3Authenticator, ct);

                currentScene = await sceneSharedContainer
                                    .SceneFactory
                                    .CreateSceneFromStreamableDirectoryAsync(TEST_SCENE, new PartitionComponent(), ct);

                await currentScene.StartUpdateLoopAsync(60, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        private static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> InstallAsync(
            IPluginSettingsContainer globalSettingsContainer,
            IPluginSettingsContainer sceneSettingsContainer,
            IWeb3Authenticator web3Authenticator,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(globalSettingsContainer,
                web3Authenticator, ct);

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            return (staticContainer, sceneSharedContainer);
        }
    }
}
