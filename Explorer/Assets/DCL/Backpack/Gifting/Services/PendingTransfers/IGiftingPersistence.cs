using System.Collections.Generic;
using DCL.Backpack.Gifting.Services.PendingTransfers;

namespace DCL.Backpack.Gifting.Services
{
    public interface IGiftingPersistence
    {
        /// <summary>
        ///     Persists the pending transfers, keyed by full URN (token instance).
        /// </summary>
        void SavePendingUrns(IReadOnlyDictionary<string, PendingTransfer> entries);

        Dictionary<string, PendingTransfer> LoadPendingUrns();
    }
}
