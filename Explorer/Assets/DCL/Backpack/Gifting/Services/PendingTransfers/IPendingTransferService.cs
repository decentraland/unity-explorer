using System.Collections.Generic;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public interface IPendingTransferService
    {
        void AddPending(string fullUrn);
        bool IsPending(string fullUrn);
        int GetPendingCount(string baseUrn);
        void Prune(IEnumerable<string> actualOwnedUrns);
    }
}