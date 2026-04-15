using DCL.Optimization.Pools;
using System.Collections.Generic;

namespace DCL.Multiplayer.Emotes
{
    public class EmotesScheduler
    {
        /// <summary>
        ///     As IDs are incremental we only need to store and play the latest one
        /// </summary>
        private readonly Dictionary<string, double> lastProcessedTimestamps = new (PoolConstants.AVATARS_COUNT);

        public void RemoveWallet(string walletId) =>
            lastProcessedTimestamps.Remove(walletId);

        public bool TryPass(string walletId, double timestamp)
        {
            if (lastProcessedTimestamps.TryGetValue(walletId, out double storedTimestamp))
            {
                if (timestamp < storedTimestamp)
                    return false;

                lastProcessedTimestamps[walletId] = timestamp;
                return true;
            }

            lastProcessedTimestamps.Add(walletId, timestamp);
            return true;
        }
    }
}
