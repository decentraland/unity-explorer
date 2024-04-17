using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    /// <summary>
    ///     This interface became redundant but I keep it if we want to mock in the future
    /// </summary>
    public interface IConnectiveRoom
    {
        enum State
        {
            Stopped,
            Starting,
            Running,
            Stopping,
        }

        void Start();

        UniTask StopAsync(CancellationToken ct);

        State CurrentState();
    }
}
