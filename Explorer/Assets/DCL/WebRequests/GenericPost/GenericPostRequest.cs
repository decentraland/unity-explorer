using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericPostRequest Initialize(in CommonArguments commonArguments, ref GenericPostArguments arguments) =>
            new (CreateWebRequest(in commonArguments, ref arguments));

        internal static UnityWebRequest CreateWebRequest(in CommonArguments commonArguments, ref GenericPostArguments arguments)
        {
            if (arguments.MultipartFormSections != null)
                return UnityWebRequest.Post(commonArguments.URL, arguments.MultipartFormSections);

            if (arguments.WWWForm != null)
                return UnityWebRequest.Post(commonArguments.URL, arguments.WWWForm);

            if (arguments.UploadHandler != null)
            {
                var request = new UnityWebRequest(commonArguments.URL, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = arguments.UploadHandler.Value.CreateUploadHandler();
                request.SetRequestHeader("Content-Type", arguments.ContentType);
                request.downloadHandler = new DownloadHandlerBuffer();
                return request;
            }

            return UnityWebRequest.Post(commonArguments.URL, arguments.PostData, arguments.ContentType);
        }

        private GenericPostRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
