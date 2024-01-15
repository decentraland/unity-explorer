using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SkyBox;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
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

        [Space]
        [SerializeField] private SkyBoxSceneData skyBoxSceneData;
        [SerializeField] private RealmLauncher realmLauncher;
        [SerializeField] private DynamicSceneLoaderSettings settings;
        [SerializeField] private DynamicSettings dynamicSettings;
        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;

        private StaticContainer? staticContainer;
        private IWeb3VerifiedAuthenticator? web3Authenticator;

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
                    foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                        plugin.Dispose();

                    if (globalWorld != null)
                        await dynamicWorldContainer.RealmController.DisposeGlobalWorldAsync().SuppressCancellationThrow();
                }

                dynamicWorldContainer?.Dispose();

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
                var identityCache = new ProxyIdentityCache(new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()));

                var web3VerifiedAuthenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                    settings.AuthWebSocketUrl,
                    settings.AuthSignatureUrl,
                    identityCache,
                    new HashSet<string>(settings.Web3WhitelistMethods));

                web3Authenticator = new ProxyVerifiedWeb3Authenticator(
                    web3VerifiedAuthenticator,
                    identityCache);

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, identityCache, web3VerifiedAuthenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer!);

                (dynamicWorldContainer, isLoaded) = await DynamicWorldContainer.CreateAsync(
                    staticContainer!,
                    scenePluginSettingsContainer,
                    ct,
                    uiToolkitRoot,
                    skyBoxSceneData,
                    settings.StaticLoadPositions,
                    settings.SceneLoadRadius,
                    dynamicSettings,
                    web3Authenticator,
                    identityCache);

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

                await UniTask.WhenAll(staticContainer!.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));
                await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));

                if (anyFailure)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory,
                    dynamicWorldContainer.EmptyScenesWorldFactory);

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
            async UniTask ChangeRealmAsync(string selectedRealm, CancellationToken ct)
            {
                IRealmController realmController = dynamicWorldContainer!.RealmController;

                if (globalWorld != null)
                    await realmController.UnloadCurrentRealmAsync();

                await UniTask.SwitchToMainThread();

                Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(settings.StartPosition);
                characterPos.y = 1f;

                staticContainer!.CharacterContainer.CharacterObject.Controller.transform.position = characterPos;

                await realmController.SetRealmAsync(URLDomain.FromString(selectedRealm), ct);
            }

            ChangeRealmAsync(selectedRealm, CancellationToken.None).Forget();
        }
    }
}
