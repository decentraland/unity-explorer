using CommunicationData.URLHelpers;
using Newtonsoft.Json;
using System;

namespace DCL.WebRequests
{
    public readonly struct CommonArguments
    {
        /// <summary>
        ///     No timeout
        /// </summary>
        public const int DEFAULT_TIMEOUT = 0;

        /// <summary>
        ///     It's an input original URL which can be transformed further in Web Requests - related systems
        /// </summary>
        public readonly URLAddress URL;

        public readonly int Timeout;
        public readonly RetryPolicy RetryPolicy;

        /// <summary>
        ///     Cleartext http to any host is permitted for this request only when set. Used solely
        ///     by the local-scene-development scene fetch, whose own module gate already vets the
        ///     dev-mode case; every other request keeps the default and gets the secure-scheme
        ///     enforcement.
        /// </summary>
        public readonly bool AllowInsecureCleartext;

        [JsonConstructor]
        public CommonArguments(URLAddress url, RetryPolicy? retryPolicy = null, int timeout = DEFAULT_TIMEOUT, bool allowInsecureCleartext = false)
        {
            URL = url;
            Timeout = timeout;
            RetryPolicy = retryPolicy ?? RetryPolicy.DEFAULT;
            AllowInsecureCleartext = allowInsecureCleartext;
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
