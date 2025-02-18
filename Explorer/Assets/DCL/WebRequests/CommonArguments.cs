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

        public const float DEFAULT_ATTEMPTS_DELAY = 0f;

        public readonly URLAddress URL;

        public readonly int AttemptsCount;

        public readonly float AttemptsDelay;

        public readonly int Timeout;

        public CommonArguments(URLAddress url, int attemptsCount = DEFAULT_ATTEMPTS_COUNT, int timeout = DEFAULT_TIMEOUT, float attemptsDelay = DEFAULT_ATTEMPTS_DELAY)
        {
            URL = url;
            AttemptsCount = attemptsCount;
            Timeout = timeout;
            AttemptsDelay = attemptsDelay;
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

        public float AttemptsDelayInMilliseconds() =>
            Mathf.Max(0, AttemptsDelay);

        public override string ToString() =>
            $"CommonArguments: {URL} with attempts {AttemptsCount} with timeout {Timeout}";
    }
}
