using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.Web3Authentication;
using ECS.Prioritization.Components;
using Global;
using UnityEngine.TestTools;
using System.Collections;
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
        private const int TARGET_FPS = 60;

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

            yield return new WaitUntil(() => staticContainer != null);
            yield return new WaitUntil(() => currentScene != null);

            // var scene = currentScene as SceneFacade;
            // yield return new WaitUntil(() => scene.sceneStateProvider.State == SceneState.Running);
            yield return new WaitForSeconds(15);

            // AudioClipsCache? clipsCache = staticContainer.ECSWorldPlugins.OfType<AudioSourcesPlugin>().FirstOrDefault().audioClipsCache;
            // yield return new WaitUntil(() => clipsCache.cache.Count > 0);
            // yield return new WaitUntil(() => clipsCache.OngoingRequests.Count == 0);
            // Assert.That(clipsCache.cache.Count, Is.EqualTo(1));

            // var audioSources = Object.FindObjectsOfType<AudioSource>();
            // Assert.That(audioSources, Has.Exactly(1).Count);
            //
            // var audioSource = audioSources[0];
            // Assert.That(audioSource.isPlaying);
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                var web3Authenticator = new RandomGeneratedWeb3Authenticator();
                await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettings, scenePluginSettings, web3Authenticator, ct);

                currentScene = await sceneSharedContainer
                                    .SceneFactory
                                    .CreateSceneFromStreamableDirectoryAsync(TEST_SCENE, new PartitionComponent(), ct);

                await currentScene.StartUpdateLoopAsync(TARGET_FPS, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception) // unhandled exception
            {
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
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(globalSettingsContainer, web3Authenticator, ct);

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            return (staticContainer, sceneSharedContainer);
        }
    }
}
