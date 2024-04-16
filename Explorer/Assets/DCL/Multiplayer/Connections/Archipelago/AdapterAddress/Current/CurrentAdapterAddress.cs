using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl.Current;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current
{
    public class CurrentAdapterAddress : ICurrentAdapterAddress
    {
        private readonly IAdapterAddresses adapterAddresses;
        private readonly ICurrentWorldAboutUrl currentWorldAboutUrl;

        public CurrentAdapterAddress(IAdapterAddresses adapterAddresses, ICurrentWorldAboutUrl currentWorldAboutUrl)
        {
            this.adapterAddresses = adapterAddresses;
            this.currentWorldAboutUrl = currentWorldAboutUrl;
        }

        public async UniTask<string> AdapterUrlAsync(CancellationToken token)
        {
            string aboutUrl = currentWorldAboutUrl.AboutUrl();
            return await adapterAddresses.AdapterUrlAsync(aboutUrl, token);
        }
    }
}
