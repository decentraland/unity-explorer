using Cysharp.Threading.Tasks;
using System.Threading;

namespace Assets.DCL.RealtimeCommunication
{
    public interface IRealtimeReports
    {
        bool IsConnected { get; }

        UniTask ConnectAsync(CancellationToken ct);

        void Report(string jsonContent);
    }
}
