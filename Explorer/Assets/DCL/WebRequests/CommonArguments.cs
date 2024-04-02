using CommunicationData.URLHelpers;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct CommonArguments
    {
        /// <summary>
        ///     No timeout
        /// </summary>
        public const int DEFAULT_TIMEOUT = 0;

        public const int DEFAULT_ATTEMPTS_COUNT = 3;

        public readonly URLAddress URL;

        public readonly int AttemptsCount;

        public readonly int Timeout;

        public readonly DownloadHandler? CustomDownloadHandler;

        public CommonArguments(string url) : this(URLAddress.FromString(url)) {
        }

        public CommonArguments(URLAddress url, DownloadHandler? customDownloadHandler = null, int attemptsCount = DEFAULT_ATTEMPTS_COUNT, int timeout = DEFAULT_TIMEOUT)
        {
            URL = url;
            CustomDownloadHandler = customDownloadHandler;
            AttemptsCount = attemptsCount;
            Timeout = timeout;
        }

        public static implicit operator CommonArguments(URLAddress url) =>
            new (url);

        public static implicit operator CommonArguments(string url) =>
            new (URLAddress.FromString(url));

        public TimeSpan TotalTimeout() =>
            Timeout == 0
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(Timeout);

        public int TotalAttempts() =>
            Mathf.Max(1, AttemptsCount);

        public override string ToString() =>
            $"CommonArguments: {URL} with attempts {AttemptsCount} with timeout {Timeout} with downloadHandler {CustomDownloadHandler}";
    }
}
