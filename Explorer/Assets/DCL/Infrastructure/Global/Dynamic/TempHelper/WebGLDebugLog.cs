using UnityEngine;

namespace Temp.Helper.WebClient
{
    public static class WebGLDebugLog
    {
        public static void Log(string location, string? message = null, string? data = null, string? hypothesisId = null)
        {
            Debug.Log($"[webgl {hypothesisId}] {location} | {message} | {data}");
        }

        public static void LogError(string location, string? message = null, string? data = null, string? hypothesisId = null)
        {
            Debug.LogError($"[webgl {hypothesisId}] {location} | {message} | {data}");
        }

        public static void LogWarning(string location, string? message = null, string? data = null, string? hypothesisId = null)
        {
            Debug.LogWarning($"[webgl {hypothesisId}] {location} | {message} | {data}");
        }
    }
}
