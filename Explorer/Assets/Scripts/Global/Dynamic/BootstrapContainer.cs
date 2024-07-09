using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    public class BootstrapContainer : IDisposable
    {
        public IAssetsProvisioner AssetsProvisioner { get; private init; }
        public IBootstrap Bootstrap { get; private set; }
        public IWeb3IdentityCache IdentityCache { get; private set; }
        public DappWeb3Authenticator Web3VerifiedAuthenticator { get; private set; }
        public ProxyVerifiedWeb3Authenticator Web3Authenticator { get; private set; }
        public IAnalyticsController? Analytics { get; private set; }

        public void Dispose()
        {
            Web3Authenticator.Dispose();
            Web3VerifiedAuthenticator.Dispose();
            IdentityCache.Dispose();
        }

        public static async UniTask<BootstrapContainer> CreateAsync(DebugSettings debugSettings, DynamicSceneLoaderSettings sceneLoaderSettings,
            AnalyticsSettings analyticsSettings, CancellationToken ct)
        {
            var container = new BootstrapContainer
            {
                AssetsProvisioner = new AddressablesProvisioner(),
            };

            (container.Bootstrap, container.Analytics) = await CreateBootstrapperAsync(debugSettings, analyticsSettings, ct, container);
            (container.IdentityCache, container.Web3VerifiedAuthenticator, container.Web3Authenticator) = CreateWeb3Dependencies(sceneLoaderSettings);

            return container;
        }

        private static async UniTask<(IBootstrap, IAnalyticsController?)> CreateBootstrapperAsync(DebugSettings debugSettings, AnalyticsSettings analyticsSettings, CancellationToken ct, BootstrapContainer container)
        {
            AnalyticsConfiguration analyticsConfig = (await container.AssetsProvisioner.ProvideMainAssetAsync(analyticsSettings.AnalyticsConfigRef, ct)).Value;
            bool enabledAnalytics = analyticsConfig.Mode != AnalyticsMode.DISABLED;

            var coreBootstrap = new Bootstrap(debugSettings.Get());
            coreBootstrap.EnableAnalytics = enabledAnalytics;

            if (enabledAnalytics)
            {
                IAnalyticsService service = analyticsConfig.Mode switch
                                            {
                                                AnalyticsMode.SEGMENT => new SegmentAnalyticsService(analyticsConfig),
                                                AnalyticsMode.DEBUG_LOG => new DebugAnalyticsService(),
                                                AnalyticsMode.DISABLED => throw new InvalidOperationException("Trying to create analytics when it is disabled"),
                                                _ => throw new ArgumentOutOfRangeException(),
                                            };

                var analyticsController = new AnalyticsController(service, analyticsConfig);

                return (new BootstrapAnalyticsDecorator(coreBootstrap, analyticsController), analyticsController);
            }

            return (coreBootstrap, IAnalyticsController.Null);
        }

        private static (LogWeb3IdentityCache identityCache, DappWeb3Authenticator web3VerifiedAuthenticator, ProxyVerifiedWeb3Authenticator web3Authenticator)
            CreateWeb3Dependencies(DynamicSceneLoaderSettings sceneLoaderSettings)
        {
            var identityCache = new LogWeb3IdentityCache(
                new ProxyIdentityCache(
                    new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(
                        new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                    )
                )
            );

            var web3VerifiedAuthenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                GetAuthUrl(sceneLoaderSettings.AuthWebSocketUrl, sceneLoaderSettings.AuthWebSocketUrlDev),
                GetAuthUrl(sceneLoaderSettings.AuthSignatureUrl, sceneLoaderSettings.AuthSignatureUrlDev),
                identityCache, new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods));

            var web3Authenticator = new ProxyVerifiedWeb3Authenticator(web3VerifiedAuthenticator, identityCache);

            return (identityCache, web3VerifiedAuthenticator, web3Authenticator);

            // Allow devUrl only in DebugBuilds (Debug.isDebugBuild is always true in Editor)
            string GetAuthUrl(string releaseUrl, string devUrl) =>
                Application.isEditor || !Debug.isDebugBuild ? releaseUrl : devUrl;
        }
    }
}
