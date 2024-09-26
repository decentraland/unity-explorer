using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using DCL.Diagnostics;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class LogAdapterAddresses : IAdapterAddresses
    {
        private readonly IAdapterAddresses origin;

        public LogAdapterAddresses(IAdapterAddresses origin)
        {
            this.origin = origin;
        }

        public string AdapterUrlAsync(string unrefinedComms)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"Original comms adapter is: {unrefinedComms}");
            unrefinedComms = origin.AdapterUrlAsync(unrefinedComms);
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"Modified comms adapter is: {unrefinedComms}");
            return unrefinedComms;
        }
    }
}
