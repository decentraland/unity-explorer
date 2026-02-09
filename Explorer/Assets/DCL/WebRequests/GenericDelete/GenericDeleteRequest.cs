using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericDeleteRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericDeleteRequest Initialize(in CommonArguments commonArguments, ref GenericPostArguments arguments)
        {
            UnityWebRequest unityWebRequest = GenericPostRequest.CreateWebRequest(in commonArguments, ref arguments);

            unityWebRequest.method = "DELETE";

            return new GenericDeleteRequest(unityWebRequest);
        }

        private GenericDeleteRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
