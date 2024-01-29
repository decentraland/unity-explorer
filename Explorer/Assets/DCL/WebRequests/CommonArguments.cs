using CommunicationData.URLHelpers;
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
    }
}
