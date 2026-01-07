using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    public static class ProfilesRequest
    {
        // JSON structure overhead for {"ids":[...]}
        private const int BASE_JSON_OVERHEAD = 10; // {"ids":[]}

        // Per-string overhead in JSON
        private const int QUOTES_PER_STRING = 2; // Opening and closing quotes
        private const int COMMA_SEPARATOR = 1; // Comma between array elements

        private static readonly ThreadSafeListPool<Profile> SINGLE_PROFILE_POOL = new (1, PoolConstants.AVATARS_COUNT);
        private static readonly ThreadSafeObjectPool<string[]> SINGLE_PROFILE_ID_POOL = new (() => new string[1], maxSize: PoolConstants.AVATARS_COUNT);

        /// <summary>
        ///     Execute GET a single profile from the catalyst
        /// </summary>
        public static async UniTask<ProfileTier?> GetAsync(IWebRequestController webRequestController, URLAddress url, string id, int version, bool retryUntilResolved,
            CancellationToken ct)
        {
            int attemptNumber = 0;
            (bool shouldRepeat, TimeSpan delay) repeatValues = (shouldRepeat: true, delay: TimeSpan.Zero);

            Profile? profile = null;

            while (repeatValues.shouldRepeat)
            {
                attemptNumber++;

                if (repeatValues.delay > TimeSpan.Zero)
                    await UniTask.Delay(repeatValues.delay, DelayType.Realtime, cancellationToken: ct);

                RetryPolicy retryPolicy = retryUntilResolved ? CatalystRetryPolicy.VALUE : RetryPolicy.NONE;

                // Suppress logging errors here as we have very custom errors handling below
                GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> response = webRequestController.GetAsync(new CommonArguments(url, retryPolicy), ct, ReportCategory.PROFILE, suppressErrors: true);

                profile = await response.CreateFromNewtonsoftJsonAsync<Profile>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, text, exception),
                    serializerSettings: RealmProfileRepository.SERIALIZER_SETTINGS);

                repeatValues = profile == null || profile.Version < version
                    ? WebRequestUtils.CanBeRepeated(attemptNumber, retryPolicy, true, null)
                    : (false, TimeSpan.Zero);
            }

            return profile;
        }

        /// <summary>
        ///     Post for a single profile
        /// </summary>
        public static async UniTask<ProfileTier?> PostSingleAsync(IWebRequestController webRequestController, URLAddress url, string id, int version, RetryPolicy retryPolicy,
            CancellationToken ct)
        {
            using PooledObject<List<Profile>> _ = SINGLE_PROFILE_POOL.Get(out List<Profile> tempProfiles);
            using PooledObject<string[]> __ = SINGLE_PROFILE_ID_POOL.Get(out string[] tempIds);
            tempIds[0] = id;

            int attemptNumber = 0;
            (bool shouldRepeat, TimeSpan delay) repeatValues = (shouldRepeat: true, delay: TimeSpan.Zero);

            ProfileTier? profile = null;

            while (repeatValues.shouldRepeat)
            {
                attemptNumber++;

                if (repeatValues.delay > TimeSpan.Zero)
                    await UniTask.Delay(repeatValues.delay, DelayType.Realtime, cancellationToken: ct);

                tempProfiles.Clear();

                await PostAsync(webRequestController, url, tempIds, tempProfiles, ct);

                // If batch contains incomplete data or an old version and still can be repeated, repeat the request with that data only
                Profile? candidate = tempProfiles.FirstOrDefault();
                profile = candidate;

                repeatValues = candidate == null || candidate.Version < version
                    ? WebRequestUtils.CanBeRepeated(attemptNumber, retryPolicy, true, null)
                    : (false, TimeSpan.Zero);
            }

            return profile;
        }

        public static async UniTask<Result<IList<T>>> PostAsync<T>(IWebRequestController webRequestController, URLAddress url, IReadOnlyCollection<string> ids, IList<T> batch, CancellationToken ct)
        {
            var uploadHandler = new BufferedStringUploadHandler(CalculateExactSize(ids.Count));

            uploadHandler.WriteString("{\"ids\":[");

            int i = 0;

            foreach (string id in ids)
            {
                uploadHandler.WriteJsonString(id);

                if (i != ids.Count - 1)
                    uploadHandler.WriteChar(',');

                i++;
            }

            uploadHandler.WriteString("]}");

            return await webRequestController.PostAsync(
                                                  url,
                                                  GenericPostArguments.CreateStringUploadHandler(uploadHandler, GenericPostArguments.JSON),
                                                  ct,
                                                  ReportCategory.PROFILE)
                                             .OverwriteFromNewtonsoftJsonAsync(batch, WRThreadFlags.SwitchToThreadPool, serializerSettings: RealmProfileRepository.SERIALIZER_SETTINGS)
                                             .SuppressToResultAsync(ReportCategory.PROFILE);
        }

        /// <summary>
        ///     Calculates the exact buffer size needed for a JSON payload with ETH wallet IDs.
        ///     Format: {"ids":["0x1234...","0x5678...",...]}.
        /// </summary>
        /// <param name="idCount">Number of ETH wallet addresses</param>
        /// <returns>Exact byte size needed for the JSON payload</returns>
        private static int CalculateExactSize(int idCount)
        {
            if (idCount <= 0)
                return BASE_JSON_OVERHEAD; // Empty array: {"ids":[]}

            // Base structure: {"ids":[]}
            int totalSize = BASE_JSON_OVERHEAD;

            // Each ID contributes:
            // - 2 bytes for quotes: ""
            // - addressLength bytes for the actual address
            // - 1 byte for comma (except last element)
            int bytesPerId = QUOTES_PER_STRING + Web3Address.ETH_ADDRESS_LENGTH;
            totalSize += bytesPerId * idCount;

            // Add commas between elements (idCount - 1 commas)
            totalSize += (idCount - 1) * COMMA_SEPARATOR;

            return totalSize;
        }
    }
}
