using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.Utilities.Extensions;
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
        [SerializeField] private string ownProfileJson;

        private ISceneFacade sceneFacade;
        private StaticContainer staticContainer;
        private IWeb3Authenticator? web3Authenticator;
        private IWeb3IdentityCache identityCache;

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
                // Initialize .NET logging ASAP since it might be used by another systems
                // Otherwise we might get exceptions in different platforms
                DotNetLoggingPlugin.Initialize();

                if (useStoredCredentials && useRealAuthentication) // avoid storing invalid credentials
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

                var memoryProfileRepository = new MemoryProfileRepository(new DefaultProfileCache());
                var webRequests = IWebRequestController.DEFAULT;

                if (!string.IsNullOrEmpty(ownProfileJson))
                {
                    var ownProfile = Profile.Create();
                    JsonUtility.FromJson<ProfileJsonDto>(ownProfileJson).CopyTo(ownProfile);
                    await memoryProfileRepository.SetAsync(ownProfile, ct);
                }

                (staticContainer, sceneSharedContainer) = await InstallAsync(new AddressablesProvisioner().WithErrorTrace(), globalPluginSettingsContainer, scenePluginSettingsContainer,
                    identityCache, dappWeb3Authenticator, identityCache, memoryProfileRepository, webRequests, ct);

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
            IAssetsProvisioner assetsProvisioner,
            IPluginSettingsContainer globalSettingsContainer,
            IPluginSettingsContainer sceneSettingsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            IEthereumApi ethereumApi,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.CreateAsync(
                assetsProvisioner,
                new DebugViewsCatalog(),
                globalSettingsContainer,
                web3IdentityProvider,
                ethereumApi,
                ct
            )!;

            if (!isLoaded)
                GameReports.PrintIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)).EnsureNotNull());

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer,
                new MVCManager(
                    new WindowStackManager(),
                    new CancellationTokenSource(), null
                ),
                identityCache,
                profileRepository,
                webRequestController,
                new IRoomHub.Fake(),
                null,
                new IMessagePipesHub.Fake()
            );

            return (staticContainer, sceneSharedContainer);
        }
    }
}
