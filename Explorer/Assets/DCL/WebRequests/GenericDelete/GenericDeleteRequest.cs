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

            // if (arguments.MultipartFormSections != null)
            //     return new GenericDeleteRequest(UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections));
            //
            // if (arguments.WWWForm != null)
            //     return new GenericDeleteRequest(UnityWebRequest.Post(commonArguments.URL, arguments.WWWForm));
            //
            // return new GenericDeleteRequest(UnityWebRequest.Post(commonArguments.URL, arguments.PostData, arguments.ContentType));
        }

        private GenericDeleteRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
