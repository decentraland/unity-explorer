using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class LogAdapterAddresses : IAdapterAddresses
    {
        private readonly IAdapterAddresses origin;
        private readonly Action<string> log;

        public LogAdapterAddresses(IAdapterAddresses origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token)
        {
            log($"AdapterUrlAsync started with url: {aboutUrl}");
            string? result = await origin.AdapterUrlAsync(aboutUrl, token);
            log($"AdapterUrlAsync finished with url: {aboutUrl} and with result: {result}");
            return result;
        }
    }
}
