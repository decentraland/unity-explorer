using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Web3Authentication;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class Web3AuthenticationPlugin : IDCLGlobalPlugin
    {
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private CancellationTokenSource? cancellationTokenSource;

        public Web3AuthenticationPlugin(IWeb3Authenticator web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder)
        {
            this.web3Authenticator = web3Authenticator;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            debugContainerBuilder.AddWidget("Web3 Authentication")
                                 .AddSingleButton("Login", Login);
        }

        private void Login()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LoginAsync(cancellationTokenSource.Token).Forget();
        }
    }
}
