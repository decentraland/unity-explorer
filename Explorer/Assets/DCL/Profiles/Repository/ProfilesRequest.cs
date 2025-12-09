using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public static class ProfilesRequest
    {
        // JSON structure overhead for {"ids":[...]}
        private const int BASE_JSON_OVERHEAD = 10; // {"ids":[]}

        // Per-string overhead in JSON
        private const int QUOTES_PER_STRING = 2; // Opening and closing quotes
        private const int COMMA_SEPARATOR = 1; // Comma between array elements

        /// <summary>
        ///     Execute GET a single profile from the catalyst
        /// </summary>
        public static async UniTask<ProfileTier?> GetAsync(IWebRequestController webRequestController, URLAddress url, string id, CancellationToken ct)
        {
            // Suppress logging errors here as we have very custom errors handling below
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> response = webRequestController.GetAsync(new CommonArguments(url, CatalystRetryPolicy.VALUE), ct, ReportCategory.PROFILE, suppressErrors: true);

            Profile? profile = await response.CreateFromNewtonsoftJsonAsync<Profile>(
                createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, text, exception),
                serializerSettings: RealmProfileRepository.SERIALIZER_SETTINGS);

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
