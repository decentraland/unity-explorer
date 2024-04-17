using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl.Current;
using DCL.WebRequests;
using ECS;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current
{
    public interface ICurrentAdapterAddress
    {
        UniTask<string> AdapterUrlAsync(CancellationToken token);

        public static ICurrentAdapterAddress NewDefault(IWebRequestController webRequestController, IRealmData realmData) =>
            new CurrentAdapterAddress(
                IAdapterAddresses.NewDefault(webRequestController),
                ICurrentWorldAboutUrl.NewDefault(realmData)
            );
    }
}
