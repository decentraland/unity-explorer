using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests.GenericHead
{
    public readonly struct GenericHeadRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        private GenericHeadRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericHeadRequest Initialize(in CommonArguments commonArguments, GenericHeadArguments arguments) =>
            new (UnityWebRequest.Head(commonArguments.URL));

        public UniTask<T> CreateFromJson<T>(
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            GenericDownloadHandlerUtils.CreateExceptionOnParseFail createCustomExceptionOnFailure = null) =>
            this.CreateFromJsonAsync<GenericHeadRequest, T>(jsonParser, threadFlags, createCustomExceptionOnFailure);
    }
}
