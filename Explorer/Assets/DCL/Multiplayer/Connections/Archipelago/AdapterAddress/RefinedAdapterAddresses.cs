using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class RefinedAdapterAddresses : IAdapterAddresses
    {
        private readonly IAdapterAddresses origin;
        private readonly string replaceRefined;

        public RefinedAdapterAddresses(IAdapterAddresses origin, string replaceRefined = "archipelago:archipelago:")
        {
            this.origin = origin;
            this.replaceRefined = replaceRefined;
        }

        public async UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token)
        {
            string result = await origin.AdapterUrlAsync(aboutUrl, token);
            return result.Replace(replaceRefined, string.Empty);
        }
    }
}
