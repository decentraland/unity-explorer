using System;
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
            UnityEngine.Debug.Log($"[GenericGetRequest] Creating UnityWebRequest with URL: {urlString}");

            // Validate URL before creating UnityWebRequest
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out Uri? validatedUri))
            {
                UnityEngine.Debug.LogError($"[GenericGetRequest] Invalid URL format: {urlString}");
                throw new ArgumentException($"Invalid URL format: {urlString}");
            }

            UnityEngine.Debug.Log($"[GenericGetRequest] URL validated - Scheme: {validatedUri.Scheme}, Host: {validatedUri.Host}, Path: {validatedUri.AbsolutePath}");

            try
            {
                var request = UnityWebRequest.Get(urlString);
                UnityEngine.Debug.Log($"[GenericGetRequest] UnityWebRequest created successfully. Request URL: {request.url}");
                return new GenericGetRequest(request);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[GenericGetRequest] Failed to create UnityWebRequest: {e.GetType().Name}: {e.Message}");
                UnityEngine.Debug.LogError($"[GenericGetRequest] Stack trace: {e.StackTrace}");
                throw;
            }
        }
    }
}
