using System.Collections.Generic;
using DCL.Backpack.Gifting.Services.PendingTransfers;

namespace DCL.Backpack.Gifting.Services
{
    public interface IGiftingPersistence
    {
        void SavePendingTransfers(
            IEnumerable<PendingTransferEntry> wearables,
            IEnumerable<PendingTransferEntry> emotes);

        (Dictionary<string, PendingTransferEntry> wearables, Dictionary<string, PendingTransferEntry> emotes) LoadPendingTransfers();
    }
}
