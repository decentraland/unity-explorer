using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3Authentication;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;
        [Space]
        [SerializeField] private UIDocument uiToolkitRoot;
        [SerializeField] private UIDocument debugUiRoot;

        [SerializeField] private RealmLauncher realmLauncher;
        [SerializeField] private DynamicSceneLoaderSettings settings;
        [SerializeField] private DynamicSettings dynamicSettings;

        private StaticContainer staticContainer;
        private DynamicWorldContainer dynamicWorldContainer;
        private GlobalWorld globalWorld;
        private DappWeb3Authenticator web3Authenticator;

        private void Awake()
        {
            realmLauncher.Initialize(settings.Realms);

            InitializationFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            async UniTaskVoid DisposeAsync()
            {
                if (dynamicWorldContainer != null)
                {
                    dynamicWorldContainer.Dispose();
                    foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                        plugin.Dispose();
                }

                if (globalWorld != null)
                    await dynamicWorldContainer.RealmController.DisposeGlobalWorldAsync().SuppressCancellationThrow();

                await UniTask.SwitchToMainThread();

                staticContainer?.Dispose();
                web3Authenticator?.Dispose();
            }

            realmLauncher.OnRealmSelected = null;
            DisposeAsync().Forget();
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                web3Authenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                    settings.AuthWebSocketUrl,
                    settings.AuthSignatureUrl);

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, web3Authenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

                (dynamicWorldContainer, isLoaded) = await DynamicWorldContainer.CreateAsync(
                    staticContainer,
                    scenePluginSettingsContainer,
                    ct,
                    uiToolkitRoot,
                    settings.StaticLoadPositions,
                    settings.SceneLoadRadius,
                    dynamicSettings,
                    web3Authenticator);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                // Initialize global plugins
                var anyFailure = false;

                void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
                {
                    if (!result.success)
                        anyFailure = true;
                }

                await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));
                await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));

                if (anyFailure)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory,
                    dynamicWorldContainer.EmptyScenesWorldFactory, staticContainer.CharacterObject);

                dynamicWorldContainer.DebugContainer.Builder.Build(debugUiRoot);
                dynamicWorldContainer.RealmController.SetupWorld(globalWorld);

                realmLauncher.OnRealmSelected += ChangeRealm;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        private void ChangeRealm(string selectedRealm)
        {
            async UniTask ChangeRealmAsync(StaticContainer globalContainer, string selectedRealm, CancellationToken ct)
            {
                if (globalWorld != null)
                    await dynamicWorldContainer.RealmController.UnloadCurrentRealmAsync();

                await UniTask.SwitchToMainThread();

                Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(settings.StartPosition);
                characterPos.y = 1f;

                globalContainer.CharacterObject.Controller.transform.position = characterPos;

                await dynamicWorldContainer.RealmController.SetRealmAsync(URLDomain.FromString(selectedRealm), ct);
            }

            ChangeRealmAsync(staticContainer, selectedRealm, CancellationToken.None).Forget();
        }
    }
}
