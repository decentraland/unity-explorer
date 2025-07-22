namespace DCL.WebRequests
{
    /// <summary>
    ///     Retries will be applied only in one of the following cases:
    ///     <list type="bullet">
    ///         <item>If Network Error is transient or the error is DNS error AND the request is idempotent (GET is always safe to retry). The retry delay will be calculated from the backoff multiplier</item>
    ///         <item>If Network Error is 429 or 503, and "Retry-After" header is specified. The retry delay will be equal to "Retry-After"</item>
    ///         <item>If Network Error is transient or the error is DNS error AND the retries are enforced. The retry delay will be calculated from the backoff multiplier</item>
    ///     </list>
    /// </summary>
    public readonly struct RetryPolicy
    {
        public const int MAX_RETRIES_COUNT = 2;

        public const int MIN_DELAY_BETWEEN_ATTEMPTS_MS = 1000;

        public const int MAX_DELAY_BETWEEN_ATTEMPTS_MS = 60000; // 1 minute

        public const int BACKOFF_MULTIPLIER = 3;

        public static readonly RetryPolicy NONE = new (0, false);

        public static readonly RetryPolicy DEFAULT = new (MAX_RETRIES_COUNT, false);

        internal readonly int minDelayBetweenAttemptsMs;
        internal readonly int backoffMultiplier;
        internal readonly int maxRetriesCount;
        internal readonly bool enforced;

        private RetryPolicy(int maxRetriesCount, bool enforce, int minDelayBetweenAttemptsMs = MIN_DELAY_BETWEEN_ATTEMPTS_MS, int backoffMultiplier = BACKOFF_MULTIPLIER)
        {
            this.maxRetriesCount = maxRetriesCount;
            this.minDelayBetweenAttemptsMs = minDelayBetweenAttemptsMs;
            this.backoffMultiplier = backoffMultiplier;
            enforced = enforce;
        }

        /// <summary>
        ///     Manually specify that retries are properly respected by the server
        /// </summary>
        /// <returns></returns>
        public static RetryPolicy Enforce(int retriesCount = MAX_RETRIES_COUNT) =>
            new (retriesCount, true);

        public static RetryPolicy WithRetries(int retriesCount, int minDelayBetweenAttemptsMs = MIN_DELAY_BETWEEN_ATTEMPTS_MS, int backoffMultiplier = BACKOFF_MULTIPLIER) =>
            new (retriesCount, false, minDelayBetweenAttemptsMs, backoffMultiplier);

        public override string ToString() =>
            $"MaxRetriesCount={maxRetriesCount}, Enforced={enforced}, MinDelayBetweenAttemptsMs={minDelayBetweenAttemptsMs}, BackoffMultiplier={backoffMultiplier}";
    }
}
