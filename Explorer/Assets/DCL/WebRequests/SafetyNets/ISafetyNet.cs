using Cysharp.Threading.Tasks;
using System.Net.Http;
using Utility.Types;

namespace DCL.WebRequests.SafetyNets
{
    public interface ISafetyNet
    {
        UniTask<Result> ExecuteAsync(HttpMethod method, string url);
    }

    public static class SafetyNetExtensions
    {
        public static UniTask<Result> ExecuteWithStringAsync(this ISafetyNet safetyNet, string? method, string url)
        {
            var httpMethod = HttpMethodFromString(method);

            return httpMethod == null
                ? UniTask.FromResult(Result.ErrorResult($"Invalid HTTP method provided to SafetyNet: {method}"))
                : safetyNet.ExecuteAsync(httpMethod, url);
        }

        private static HttpMethod? HttpMethodFromString(string? method) =>
            method?.ToUpper() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                _ => null,
            };
    }
}
