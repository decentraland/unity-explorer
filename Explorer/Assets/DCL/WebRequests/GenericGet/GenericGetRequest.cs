using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericGetRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        private GenericGetRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericGetRequest Initialize(in CommonArguments commonArguments, GenericGetArguments arguments) =>
            new (UnityWebRequest.Get(commonArguments.URL));

        /// <summary>
        /// Disposes the request, parses the response text as a JSON and overwrites the target object with the parsed data.
        /// </summary>
        /// <returns>UniTask with parsed DTO result</returns>
        public UniTask<T> CreateFromJson<T>(
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            GenericDownloadHandlerUtils.CreateExceptionOnParseFail? createCustomExceptionOnFailure = null) =>
            this.CreateFromJsonAsync<GenericGetRequest, T>(jsonParser, threadFlags, createCustomExceptionOnFailure);
    }
}
