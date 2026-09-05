using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericDeleteRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericDeleteRequest Initialize(string url, ref GenericPostArguments arguments)
        {
            UnityWebRequest unityWebRequest = GenericPostRequest.CreateWebRequest(url, ref arguments);

            unityWebRequest.method = "DELETE";

            return new GenericDeleteRequest(unityWebRequest);
        }

        private GenericDeleteRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
