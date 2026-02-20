using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericPostRequest Initialize(string effectiveUrl, ref GenericPostArguments arguments) =>
            new (CreateWebRequest(effectiveUrl, ref arguments));

        internal static UnityWebRequest CreateWebRequest(string effectiveUrl, ref GenericPostArguments arguments)
        {
            if (arguments.MultipartFormSections != null)
                return UnityWebRequest.Post(effectiveUrl, arguments.MultipartFormSections);

            if (arguments.WWWForm != null)
                return UnityWebRequest.Post(effectiveUrl, arguments.WWWForm);

            if (arguments.UploadHandler != null)
            {
                var request = new UnityWebRequest(effectiveUrl, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = arguments.UploadHandler.Value.CreateUploadHandler();
                request.SetRequestHeader("Content-Type", arguments.ContentType);
                request.downloadHandler = new DownloadHandlerBuffer();
                return request;
            }

            return UnityWebRequest.Post(effectiveUrl, arguments.PostData, arguments.ContentType);
        }

        private GenericPostRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
