using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.Web3Authentication.Authenticators;
using DCL.Web3Authentication.Identities;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace Global.Static
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class StaticSceneLauncher : MonoBehaviour
    {
        private const string SCENES_UI_ROOT_CANVAS = "ScenesUIRootCanvas";
        private const string SCENES_UI_STYLE_SHEET = "ScenesUIStyleSheet";

        [SerializeField] private SceneLauncher sceneLauncher;
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;

        private ISceneFacade sceneFacade;
        private StaticContainer staticContainer;
        private IWeb3Authenticator? web3Authenticator;
        private UIDocument scenesUIcanvas;
        private StyleSheet scenesUIStyleSheet;

        private async void Awake()
        {
            scenesUIcanvas = Instantiate(await Addressables.LoadAssetAsync<GameObject>(SCENES_UI_ROOT_CANVAS)).GetComponent<UIDocument>();
            scenesUIStyleSheet = await Addressables.LoadAssetAsync<StyleSheet>(SCENES_UI_STYLE_SHEET);

            InitializationFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            staticContainer?.Dispose();
            web3Authenticator?.Dispose();
        }

        public async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                var identityCache = new MemoryWeb3IdentityCache();

                web3Authenticator = new ProxyWeb3Authenticator(new RandomGeneratedWeb3Authenticator(),
                    identityCache);
                await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettingsContainer, scenePluginSettingsContainer,
                    scenesUIcanvas, scenesUIStyleSheet, identityCache, ct);
                sceneLauncher.Initialize(sceneSharedContainer, destroyCancellationToken);
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
            UIDocument scenesUiRoot,
            StyleSheet scenesUiStyleSheet,
            IWeb3IdentityCache web3IdentityProvider,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(globalSettingsContainer,
                scenesUiRoot, scenesUiStyleSheet,
                web3IdentityProvider, ct);

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            return (staticContainer, sceneSharedContainer);
        }
    }
}
