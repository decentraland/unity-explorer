using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPutRequest : ITypedWebRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPutRequest Initialize(in CommonArguments commonArguments, GenericPutArguments arguments)
        {
            var unityWebRequest = arguments.MultipartFormSections != null ?
                UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections)
                : UnityWebRequest.Post(commonArguments.URL, arguments.PutData, arguments.ContentType);

            unityWebRequest.method = "PUT";

            return new GenericPutRequest(unityWebRequest);
        }

        private GenericPutRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        public UniTask<T> OverwriteFromJson<T>(
            T targetObject,
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread) where T: class =>
            GenericDownloadHandlerUtils.OverwriteFromJson(UnityWebRequest, targetObject, jsonParser, threadFlags);

        public UniTask<T> CreateFromJson<T>(
            WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread) =>
            GenericDownloadHandlerUtils.CreateFromJson<T>(UnityWebRequest, jsonParser, threadFlags);

        public byte[] GetDataCopy() =>
            GenericDownloadHandlerUtils.GetDataCopy(UnityWebRequest);
    }
}
