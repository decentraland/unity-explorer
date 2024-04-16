using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using Global;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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

        //[UnityTest]
        [Retry(5)]
        public IEnumerator ShouldCreateAudioSourcesForSDKDanceFloorScene()
        {
            InitializationFlowAsync(tearDownCts.Token).Forget();

            yield return new WaitUntil(() => staticContainer != null);
            yield return new WaitUntil(() => currentScene != null);

            var scene = currentScene as SceneFacade;
            yield return new WaitUntil(() => scene!.sceneStateProvider.State == SceneState.Running);

            AudioClipsCache clipsCache = staticContainer.ECSWorldPlugins.OfType<AudioSourcesPlugin>().FirstOrDefault().audioClipsCache;

            yield return new WaitUntil(() => clipsCache.cache.Count > 0);
            yield return new WaitUntil(() => clipsCache.OngoingRequests.Count == 0);
            Assert.That(clipsCache.cache.Count, Is.EqualTo(1));

            var audioSources = Object.FindObjectsOfType<AudioSource>();
            Assert.That(audioSources.Length, Is.EqualTo(1));
            Assert.That(audioSources[0].isPlaying);
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                var identityCache = new MemoryWeb3IdentityCache();

                var web3Authenticator = new ProxyWeb3Authenticator(new RandomGeneratedWeb3Authenticator(), identityCache);
                await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettings, scenePluginSettings, identityCache,
                    Substitute.For<IEthereumApi>(), Substitute.For<IProfileRepository>(), ct);

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

        private static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> InstallAsync(IPluginSettingsContainer globalSettingsContainer,
            IPluginSettingsContainer sceneSettingsContainer,
            IWeb3IdentityCache web3IdentityCache,
            IEthereumApi ethereumApi,
            IProfileRepository profileRepository,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(
                globalSettingsContainer,
                web3IdentityCache,
                ethereumApi,
                ct);

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer,
                Substitute.For<IMVCManager>(), web3IdentityCache, profileRepository, IWebRequestController.DEFAULT, new IRoomHub.Fake(),
                Substitute.For<IRealmData>(), new IMessagePipesHub.Fake());

            return (staticContainer, sceneSharedContainer);
        }
    }
}
