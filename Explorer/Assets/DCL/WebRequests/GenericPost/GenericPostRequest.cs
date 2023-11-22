using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostRequest : ITypedWebRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPostRequest Initialize(in CommonArguments commonArguments, GenericPostArguments arguments)
        {
            if (arguments.MultipartFormSections != null)
                return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections));

            return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.PostData, arguments.ContentType));
        }

        private GenericPostRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        public UniTask<T> OverwriteFromJson<T>(
            T targetObject,
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread) where T: class =>
            GenericDownloadHandlerUtils.OverwriteFromJsonAsync(UnityWebRequest, targetObject, jsonParser, threadFlags);

        public UniTask<T> CreateFromJson<T>(
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread) =>
            GenericDownloadHandlerUtils.CreateFromJsonAsync<T>(UnityWebRequest, jsonParser, threadFlags);

        public byte[] GetDataCopy() =>
            GenericDownloadHandlerUtils.GetDataCopy(UnityWebRequest);
    }
}
