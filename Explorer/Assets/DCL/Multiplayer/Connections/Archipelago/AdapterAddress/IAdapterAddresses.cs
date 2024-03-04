using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public interface IAdapterAddresses
    {
        UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token);
    }
}
