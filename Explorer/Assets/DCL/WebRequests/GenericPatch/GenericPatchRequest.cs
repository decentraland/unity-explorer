using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPatchRequest : ITypedWebRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPatchRequest Initialize(in CommonArguments commonArguments, GenericPatchArguments arguments)
        {
            var unityWebRequest = arguments.MultipartFormSections != null ?
                UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections)
                : UnityWebRequest.Post(commonArguments.URL, arguments.PatchData, arguments.ContentType);

            unityWebRequest.method = "PATCH";

            return new GenericPatchRequest(unityWebRequest);
        }

        private GenericPatchRequest(UnityWebRequest unityWebRequest)
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
