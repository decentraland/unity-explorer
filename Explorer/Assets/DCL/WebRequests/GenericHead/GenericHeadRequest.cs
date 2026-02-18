using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericHeadRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => true;

        private GenericHeadRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericHeadRequest Initialize(string url, ref GenericHeadArguments arguments) =>
            new (UnityWebRequest.Head(url));
    }
}
