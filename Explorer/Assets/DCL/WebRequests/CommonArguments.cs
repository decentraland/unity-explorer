using CommunicationData.URLHelpers;
using System;

namespace DCL.WebRequests
{
    public readonly struct CommonArguments
    {
        /// <summary>
        ///     No timeout
        /// </summary>
        public const int DEFAULT_TIMEOUT = 0;

        public readonly URLAddress URL;

        public readonly int Timeout;
        public readonly RetryPolicy RetryPolicy;

        public CommonArguments(URLAddress url, RetryPolicy? retryPolicy = null, int timeout = DEFAULT_TIMEOUT)
        {
            URL = url;
            Timeout = timeout;
            RetryPolicy = retryPolicy ?? RetryPolicy.DEFAULT;
        }

        public static implicit operator CommonArguments(URLAddress url) =>
            new (url);

        public static implicit operator CommonArguments(string url) =>
            new (URLAddress.FromString(url));

        public TimeSpan TotalTimeout() =>
            Timeout == 0
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(Timeout);

        public override string ToString() =>
            $"CommonArguments: {URL} with retries {RetryPolicy} with timeout {Timeout}";
    }
}
