using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPutRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPutRequest Initialize(in CommonArguments commonArguments, GenericPutArguments arguments)
        {
            UnityWebRequest unityWebRequest = arguments.MultipartFormSections != null
                ? UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections)
                : UnityWebRequest.Post(commonArguments.URL, arguments.PutData, arguments.ContentType);

            unityWebRequest.method = "PUT";

            return new GenericPutRequest(unityWebRequest);
        }

        private GenericPutRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
