using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericHeadRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        private GenericHeadRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericHeadRequest Initialize(in CommonArguments commonArguments, GenericHeadArguments arguments) =>
            new (UnityWebRequest.Head(commonArguments.URL));
    }
}
