using Cysharp.Threading.Tasks;
using System;
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
            result = result.Replace(replaceRefined, string.Empty);

            result = RemoveHttpsPreInfo(result);
            result = RemoveWssPreInfo(result);

            return result;
        }

        private string RemoveHttpsPreInfo(string url)
        {
            int index = url.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }

        private string RemoveWssPreInfo(string url)
        {
            int index = url.IndexOf("wss://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }
    }
}
