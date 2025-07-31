using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     It is the same as <see cref="GenericGetRequest" /> but enables profiling in separation
    /// </summary>
    public readonly struct PartialDownloadRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => true;

        private PartialDownloadRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static PartialDownloadRequest Initialize(in CommonArguments commonArguments, GenericGetArguments arguments) =>
            new (UnityWebRequest.Get(commonArguments.URL));
    }
}
