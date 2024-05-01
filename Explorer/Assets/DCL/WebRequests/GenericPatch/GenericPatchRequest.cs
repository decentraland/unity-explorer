using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPatchRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPatchRequest Initialize(in CommonArguments commonArguments, GenericPatchArguments arguments)
        {
            UnityWebRequest unityWebRequest = arguments.MultipartFormSections != null
                ? UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections)
                : UnityWebRequest.Post(commonArguments.URL, arguments.PatchData, arguments.ContentType);

            unityWebRequest.method = "PATCH";

            return new GenericPatchRequest(unityWebRequest);
        }

        private GenericPatchRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
