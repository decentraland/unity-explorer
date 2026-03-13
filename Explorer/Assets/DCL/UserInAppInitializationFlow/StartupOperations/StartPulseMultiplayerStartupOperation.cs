using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class StartPulseMultiplayerStartupOperation : StartUpOperationBase
    {
        private readonly PulseMultiplayerService service;

        public StartPulseMultiplayerStartupOperation(PulseMultiplayerService service)
        {
            this.service = service;
        }

        protected override UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct) =>
            service.ConnectAsync(ct);
    }
}
