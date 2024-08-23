using UnityEngine.Networking;

namespace DCL.WebRequests.GenericDelete
{
    public readonly struct GenericDeleteRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericDeleteRequest Initialize(in CommonArguments commonArguments, GenericDeleteArguments arguments)
        {
            UnityWebRequest unityWebRequest = arguments.MultipartFormSections != null
                ? UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections)!
                : UnityWebRequest.Post(commonArguments.URL, arguments.DeleteData, arguments.ContentType)!;

            unityWebRequest.method = "DELETE";

            return new GenericDeleteRequest(unityWebRequest);
        }

        private GenericDeleteRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
