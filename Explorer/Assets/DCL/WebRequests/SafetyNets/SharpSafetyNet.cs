using Cysharp.Threading.Tasks;
using System;
using System.Net.Http;
using Utility.Types;

namespace DCL.WebRequests.SafetyNets
{
    public class SharpSafetyNet : ISafetyNet
    {
        private readonly HttpClient client;

        public SharpSafetyNet() : this(new HttpClient()) { }

        public SharpSafetyNet(HttpClient client)
        {
            this.client = client;
        }

        public async UniTask<Result> ExecuteAsync(HttpMethod method, string url)
        {
            try
            {
                var result = await client.SendAsync(new HttpRequestMessage(method, url))!;

                return result.IsSuccessStatusCode
                    ? Result.SuccessResult()
                    : Result.ErrorResult(result.ReasonPhrase ?? $"Unknown error: {result.StatusCode}");
            }
            catch (Exception e)
            {
                return Result.ErrorResult($"Exception thrown: {e.Message}");
            }
        }
    }
}
