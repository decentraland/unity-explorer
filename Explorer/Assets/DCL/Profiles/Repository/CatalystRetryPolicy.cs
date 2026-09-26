using DCL.WebRequests;

namespace DCL.Profiles
{
    /// <summary>
    ///     Catalysts are prone to huge replication delays.
    ///     This special non-strict policy allows more retry attempts with bigger delays between them.
    /// </summary>
    public static class CatalystRetryPolicy
    {
        /// <summary>
        ///     k=1 → 2000 × 2^0 = 2000 ms (2.00 s) <br />
        ///     k=2 → 2000 × 2^1 = 4000 ms (4.00 s) <br />
        ///     k=3 → 2000 × 2^2 = 8000 ms (8.00 s) <br />
        ///     k=4 → 2000 × 2^3 = 16000 ms (16.00 s) <br />
        ///     k=5 → 2000 × 2^4 = 32000 ms (32.00 s) <br />
        ///     k=6 → 2000 × 2^5 = 64000 ms → capped at 60000 ms (60.00 s) <br />
        ///     Total wait time ≈ 122 s (~2.0 minutes)
        /// </summary>
        public static readonly RetryPolicy VALUE = RetryPolicy.Enforce(6, 2000, 2, IWebRequestController.IGNORE_NOT_FOUND);
    }

    /// <summary>
    ///     Aligned to the polling window in the asset-bundle-registry
    /// </summary>
    public static class CentralizedProfileRetryPolicy
    {
        /// <summary>
        ///     Never returns 404, returns an empty array as GET is not supported
        /// </summary>
        public static readonly RetryPolicy VALUE = RetryPolicy.Enforce(3, 1500, 2);
    }
}
