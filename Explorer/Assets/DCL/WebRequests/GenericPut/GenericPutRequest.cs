using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPutRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericPutRequest Initialize(in CommonArguments commonArguments, ref GenericPostArguments arguments)
        {
            UnityWebRequest unityWebRequest = GenericPostRequest.CreateWebRequest(in commonArguments, ref arguments);

            unityWebRequest.method = "PUT";

            return new GenericPutRequest(unityWebRequest);
        }

        private GenericPutRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
