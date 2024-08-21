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

        public string AdapterUrlAsync(string unrefinedComms)
        {
            log($"Original comms adapter is: {unrefinedComms}");
            unrefinedComms = origin.AdapterUrlAsync(unrefinedComms);
            log($"Modified comms adapter is: {unrefinedComms}");
            return unrefinedComms;
        }
    }
}
