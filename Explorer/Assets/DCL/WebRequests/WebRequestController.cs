using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using System;
using System.Diagnostics;
using UnityEngine.Networking;
using Utility.Multithreading;
using Debug = UnityEngine.Debug;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public WebRequestController(IWeb3IdentityCache web3IdentityCache) : this(IWebRequestsAnalyticsContainer.DEFAULT, web3IdentityCache) { }

        public WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            int attemptsLeft = envelope.CommonArguments.TotalAttempts();

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

            while (attemptsLeft > 0)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

                try
                {
                    await request.WithAnalyticsAsync(analyticsContainer, request.SendRequest(envelope.Ct));

                    // if no exception is thrown Request is successful and the continuation op can be executed
                    return await op.ExecuteAsync(request, envelope.Ct);

                    // After the operation is executed, the flow may continue in the background thread
                }
                catch (UnityWebRequestException exception)
                {
                    // No result can be concluded in this case
                    if (envelope.ShouldIgnoreResponseError(exception.UnityWebRequest!))
                        return default(TResult);

                    attemptsLeft--;

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportCategory,
                            $"Exception occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                            + $"Attempt Left: {attemptsLeft}"
                        );

                    if (exception.Message.Contains("Cannot connect to destination host"))
                    {
                        Debug.Log($"JUANI ERROR RESPONSE CODE {exception.ResponseCode} for url: {envelope.CommonArguments.URL}");
                        Debug.Log($"JUANI ERROR  RESPONSE HEADERS {exception.Result}");

                        if (exception.ResponseHeaders != null)
                        {
                            foreach (var keyValue in exception.ResponseHeaders) { Debug.Log($"JUANI ERROR RESPONSE HEADERS {keyValue.Key} {keyValue.Value}"); }
                        }

                        Debug.Log("JUANI ERROR ACA ESTA LA EXCEPCION, vamos a ponerle un await");
                        DirectCurlRequestAndPrint(envelope.CommonArguments.URL);
                        await UniTask.Delay(TimeSpan.FromSeconds(1));
                    }

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }

        private static void DirectCurlRequestAndPrint(string url)
        {
            bool isUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                         && (uriResult!.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (isUrl == false)
            {
                Debug.Log($"JUANI ERROR CURL string is not url: {url}");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "curl", // Path to the Zsh executable
                Arguments = url, // Command to execute
                UseShellExecute = false,
                RedirectStandardOutput = true, // To capture the output
                RedirectStandardError = true, // To capture any errors
            };

            // Create and start the process
            Process proc = new Process { StartInfo = startInfo };
            proc.Start();
            proc.WaitForExit();

            string output = proc.StandardOutput.ReadToEnd();
            string errors = proc.StandardError.ReadToEnd();

            Debug.Log($"JUANI ERROR CURL output: {output}; error: {errors}");
        }
    }
}
