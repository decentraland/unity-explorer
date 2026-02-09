using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPatchRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => false;

        internal static GenericPatchRequest Initialize(in CommonArguments commonArguments, ref GenericPostArguments arguments)
        {
            UnityWebRequest unityWebRequest = GenericPostRequest.CreateWebRequest(in commonArguments, ref arguments);

            unityWebRequest.method = "PATCH";

            return new GenericPatchRequest(unityWebRequest);
        }

        private GenericPatchRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }
    }
}
