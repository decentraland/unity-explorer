using DCL.Optimization.Pools;
using System.Collections.Generic;

namespace DCL.Multiplayer.Emotes
{
    public class EmotesDeduplication
    {
        /// <summary>
        ///     As IDs are incremental we only need to store and play the latest one
        /// </summary>
        private readonly Dictionary<string, float> lastProcessedTimestamps = new (PoolConstants.AVATARS_COUNT);

        public void RemoveWallet(string walletId) =>
            lastProcessedTimestamps.Remove(walletId);

        public bool TryPass(string walletId, float timestamp)
        {
            if (lastProcessedTimestamps.TryGetValue(walletId, out float storedTimestamp))
            {
                lastProcessedTimestamps[walletId] = timestamp;
                return timestamp > storedTimestamp;
            }

            lastProcessedTimestamps.Add(walletId, timestamp);
            return true;
        }
    }
}
