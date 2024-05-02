using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        internal static GenericPostRequest Initialize(in CommonArguments commonArguments, GenericPostArguments arguments)
        {
            if (arguments.MultipartFormSections != null)
                return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections));

            if (arguments.WWWForm != null)
                return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.WWWForm));

            return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.PostData, arguments.ContentType));
        }

        private GenericPostRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
