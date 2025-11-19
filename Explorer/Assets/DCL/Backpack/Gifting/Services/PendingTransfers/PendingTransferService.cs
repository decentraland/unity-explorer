using System.Collections.Generic;
using DCL.Diagnostics;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public class PendingTransferService : IPendingTransferService
    {
        private readonly IGiftingPersistence persistence;
        private readonly HashSet<string> pendingFullUrns;

        public PendingTransferService(IGiftingPersistence persistence)
        {
            this.persistence = persistence;
            pendingFullUrns = persistence.LoadPendingUrns();
        }

        public void AddPending(string fullUrn)
        {
            if (pendingFullUrns.Add(fullUrn))
                persistence.SavePendingUrns(pendingFullUrns);
        }

        public bool IsPending(string fullUrn)
        {
            return pendingFullUrns.Contains(fullUrn);
        }

        public int GetPendingCount(string baseUrn)
        {
            int count = 0;

            foreach (string pending in pendingFullUrns)
            {
                if (pending.StartsWith(baseUrn) &&
                    (pending.Length == baseUrn.Length || pending[baseUrn.Length] == ':'))
                {
                    count++;
                }
            }

            return count;
        }

        public void Prune(IEnumerable<string> actualOwnedUrns)
        {
            var actualSet = new HashSet<string>(actualOwnedUrns);
            var toRemove = new List<string>();

            foreach (string? pending in pendingFullUrns)
            {
                if (!actualSet.Contains(pending))
                    toRemove.Add(pending);
            }

            if (toRemove.Count > 0)
            {
                foreach (string? item in toRemove) pendingFullUrns.Remove(item);
                persistence.SavePendingUrns(pendingFullUrns);
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {toRemove.Count} gifts.");
            }
        }
    }
}