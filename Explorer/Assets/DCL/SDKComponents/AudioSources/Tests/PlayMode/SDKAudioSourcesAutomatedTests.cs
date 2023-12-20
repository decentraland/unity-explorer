using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.Web3Authentication;
using ECS.Prioritization.Components;
using Global;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources.Tests.PlayMode
{
    public class SDKAudioSourcesAutomatedTests
    {
        private string testScene = "Assets/Scenes/StaticSceneLoader.unity";
        private PluginSettingsContainer globalPluginSettingsContainer = AssetDatabase.LoadAssetAtPath<PluginSettingsContainer>(
            "Assets/DCL/PluginSystem/Global/Global Plugins Settings.asset");
        private PluginSettingsContainer scenePluginSettingsContainer = AssetDatabase.LoadAssetAtPath<PluginSettingsContainer>(
            "Assets/DCL/PluginSystem/World/World Plugins Container.asset");

        private StaticContainer staticContainer;

        private CancellationTokenSource tearDownCts = new ();

        [UnityTearDown]
        public void TearDown()
        {
            tearDownCts.Cancel();
            tearDownCts.Dispose();

            staticContainer.Dispose();
        }

        [UnityTest]
        public IEnumerator VV()
        {
            InitializationFlowAsync(tearDownCts.Token).Forget();

            yield return new WaitForSeconds(1);
            Assert.IsTrue(true);
        }

        public async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                var web3Authenticator = new FakeWeb3Authenticator();
                await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettingsContainer, scenePluginSettingsContainer,
                    web3Authenticator, ct);

                var currentScene = await sceneSharedContainer
                                        .SceneFactory
                                        .CreateSceneFromStreamableDirectoryAsync("Dance-floor", new PartitionComponent(), ct);

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

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> InstallAsync(
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

        public IEnumerator VVV()
        {
            // Load the scene asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(testScene, LoadSceneMode.Single);

            // Wait until the scene is loaded
            while (!asyncLoad.isDone)
                yield return null;

            yield return new WaitForSeconds(10000);

            // Pass the test if this point is reached
            Assert.IsTrue(true);
        }
    }
}
