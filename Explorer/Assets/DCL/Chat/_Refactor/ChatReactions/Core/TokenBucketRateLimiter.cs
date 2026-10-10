using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Allocation-free token-bucket rate limiter.
    /// Call <see cref="Refill"/> once per tick, then <see cref="TryConsume"/> to gate an action.
    /// Capacity equals the rate (one second's worth of tokens).
    /// </summary>
    internal sealed class TokenBucketRateLimiter
    {
        private float tokens;

        public TokenBucketRateLimiter(float initialTokens)
        {
            tokens = initialTokens;
        }

        public void Refill(float dt, float tokensPerSecond)
        {
            tokens = Mathf.Min(tokens + dt * tokensPerSecond, tokensPerSecond);
        }

        public bool TryConsume()
        {
            if (tokens < 1f)
                return false;

            tokens -= 1f;
            return true;
        }
    }
}
