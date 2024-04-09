using DCL.Optimization.Pools;
using System.Collections.Generic;

namespace DCL.Multiplayer.Emotes
{
    public class EmotesDeduplication
    {
        /// <summary>
        ///     As IDs are incremental we only need to store and play the latest one
        /// </summary>
        private readonly Dictionary<string, uint> lastProcessedIds = new (PoolConstants.AVATARS_COUNT);

        public void RemoveWallet(string walletId) =>
            lastProcessedIds.Remove(walletId);

        public bool TryPass(string walletId, uint incrementalId)
        {
            if (lastProcessedIds.TryGetValue(walletId, out uint storedId))
            {
                lastProcessedIds[walletId] = incrementalId;
                return incrementalId > storedId;
            }

            lastProcessedIds.Add(walletId, incrementalId);
            return true;
        }
    }
}
