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

        public UniTask<T> CreateFromJson<T>(
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            GenericDownloadHandlerUtils.CreateExceptionOnParseFail createCustomExceptionOnFailure = null) =>
            this.CreateFromJsonAsync<GenericGetRequest, T>(jsonParser, threadFlags, createCustomExceptionOnFailure);
    }
}
