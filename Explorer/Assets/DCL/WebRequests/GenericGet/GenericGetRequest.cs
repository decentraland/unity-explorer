using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericGetRequest : ITypedWebRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        private GenericGetRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericGetRequest Initialize(in CommonArguments commonArguments, GenericGetArguments arguments) =>
            new (UnityWebRequest.Get(commonArguments.URL));

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
