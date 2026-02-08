using System;
using Temp.Helper.WebClient;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericGetRequest : ITypedWebRequest, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest
    {
        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => true;

        private GenericGetRequest(UnityWebRequest unityWebRequest)
        {
            UnityWebRequest = unityWebRequest;
        }

        internal static GenericGetRequest Initialize(in CommonArguments commonArguments, GenericGetArguments arguments)
        {
            string urlString = commonArguments.URL.Value;

            // Validate URL before creating UnityWebRequest
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out Uri? validatedUri))
            {
                WebGLDebugLog.LogError($"[GenericGetRequest] Invalid URL format: {urlString}");
                throw new ArgumentException($"Invalid URL format: {urlString}");
            }

            try
            {
                var request = UnityWebRequest.Get(urlString);
                return new GenericGetRequest(request);
            }
            catch (Exception e)
            {
                WebGLDebugLog.LogError($"[GenericGetRequest] Failed to create UnityWebRequest: {e.GetType().Name}: {e.Message}");
                WebGLDebugLog.LogError($"[GenericGetRequest] Stack trace: {e.StackTrace}");
                throw;
            }
        }
    }
}
