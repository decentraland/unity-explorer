using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericPostRequest Initialize(in CommonArguments commonArguments, GenericPostArguments arguments)
        {
            if (arguments.MultipartFormSections != null)
                return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections));

            if (arguments.WWWForm != null)
                return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.WWWForm));

            if (arguments.UploadHandler != null)
            {
                var request = new UnityWebRequest(commonArguments.URL, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = arguments.UploadHandler;
                request.SetRequestHeader("Content-Type", arguments.ContentType);
                request.downloadHandler = new DownloadHandlerBuffer();
                return new GenericPostRequest(request);
            }

            return new GenericPostRequest(UnityWebRequest.Post(commonArguments.URL, arguments.PostData, arguments.ContentType));
        }

        private GenericPostRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
