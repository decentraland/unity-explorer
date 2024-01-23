using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.PluginSystem;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global.Static
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class StaticSceneLauncher : MonoBehaviour
    {
        [SerializeField] private SceneLauncher sceneLauncher;
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;
        [SerializeField] private bool useRealAuthentication;
        [SerializeField] private bool useStoredCredentials;
        [SerializeField] private string authenticationServerUrl;
        [SerializeField] private string authenticationSignatureUrl;
        [SerializeField] private string[] ethWhitelistMethods;

        private ISceneFacade sceneFacade;
        private StaticContainer staticContainer;
        private IWeb3Authenticator? web3Authenticator;

        private void Awake()
        {
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
                IWeb3IdentityCache identityCache;

                if (useStoredCredentials

                    // avoid storing invalid credentials
                    && useRealAuthentication)
                    identityCache = new ProxyIdentityCache(new MemoryWeb3IdentityCache(),
                        new PlayerPrefsIdentityProvider(new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()));
                else
                    identityCache = new MemoryWeb3IdentityCache();

                var dappWeb3Authenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                    authenticationServerUrl, authenticationSignatureUrl,
                    identityCache,
                    new HashSet<string>(ethWhitelistMethods));

                IWeb3Authenticator web3Authenticator;

                if (useRealAuthentication)
                    web3Authenticator = new ProxyWeb3Authenticator(dappWeb3Authenticator,
                        identityCache);
                else
                    web3Authenticator = new ProxyWeb3Authenticator(new RandomGeneratedWeb3Authenticator(),
                        identityCache);

                if (useRealAuthentication)
                {
                    if (identityCache.Identity is { IsExpired: true })
                        await web3Authenticator.LoginAsync(ct);
                }
                else
                    await web3Authenticator.LoginAsync(ct);

                SceneSharedContainer sceneSharedContainer;

                (staticContainer, sceneSharedContainer) = await InstallAsync(globalPluginSettingsContainer, scenePluginSettingsContainer,
                    identityCache, dappWeb3Authenticator, ct);

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
            IWeb3IdentityCache web3IdentityProvider,
            IEthereumApi ethereumApi,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(globalSettingsContainer, web3IdentityProvider, ethereumApi, ct);

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            return (staticContainer, sceneSharedContainer);
        }
    }
}
