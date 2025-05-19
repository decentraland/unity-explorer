using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SocialService;
using DCL.Utilities;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class SocialServicesPlugin : IDCLGlobalPluginWithoutSettings
    {
        // TODO why ObjectProxy, is it a circular dependency?
        private readonly ObjectProxy<IRPCSocialServices> socialServicesRPCProxy;
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IAppArgs appArgs;

        private RPCSocialServices? socialServicesRPC;
        private CancellationTokenSource cts = new ();

        public SocialServicesPlugin(
            ObjectProxy<IRPCSocialServices> socialServicesRPCProxy,
            IDecentralandUrlsSource dclUrlSource,
            IWeb3IdentityCache web3IdentityCache,
            ISocialServiceEventBus socialServiceEventBus,
            IAppArgs appArgs)
        {
            this.socialServicesRPCProxy = socialServicesRPCProxy;
            this.dclUrlSource = dclUrlSource;
            this.web3IdentityCache = web3IdentityCache;
            this.socialServiceEventBus = socialServiceEventBus;
            this.appArgs = appArgs;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct)
        {
            // We need to restart the connection to the service as identity changes
            // since that affects which friends the user can access
            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReInitializeRpcClient;

            socialServicesRPC = new RPCSocialServices(GetApiUrl(), web3IdentityCache, socialServiceEventBus);
            socialServicesRPCProxy.SetObject(socialServicesRPC);
        }

        public void Dispose()
        {
            socialServicesRPC?.Dispose();
            web3IdentityCache.OnIdentityCleared -= DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged -= ReInitializeRpcClient;
        }

        private void ReInitializeRpcClient()
        {
            cts = cts.SafeRestart();
            ReconnectRpcClientAsync(cts.Token).Forget();
            return;

            async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
            {
                if (socialServicesRPC == null) return;

                try
                {
                    await socialServicesRPC.DisconnectAsync(ct);
                    await socialServicesRPC.EnsureRpcConnectionAsync(ct);
                }
                catch (Exception e) when (e is not OperationCanceledException) { }

                socialServiceEventBus.SendTransportReconnectedNotification();
            }
        }


        private void DisconnectRpcClient()
        {
            cts = cts.SafeRestart();
            DisconnectRpcClientAsync(cts.Token).Forget();
            return;

            async UniTaskVoid DisconnectRpcClientAsync(CancellationToken ct)
            {
                if (socialServicesRPC == null) return;

                try { await socialServicesRPC.DisconnectAsync(ct); }
                catch (Exception e) when (e is not OperationCanceledException) { }
            }
        }

        private URLAddress GetApiUrl()
        {
            string url = dclUrlSource.Url(DecentralandUrl.ApiFriends);

            if (appArgs.TryGetValue(AppArgsFlags.FRIENDS_API_URL, out string? urlFromArgs))
                url = urlFromArgs!;

            return URLAddress.FromString(url);
        }


    }
}
