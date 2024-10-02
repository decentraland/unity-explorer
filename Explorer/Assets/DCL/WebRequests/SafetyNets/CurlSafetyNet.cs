using Cysharp.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Net.Http;
using Utility.Multithreading;
using Utility.Types;

namespace DCL.WebRequests.SafetyNets
{
    public class CurlSafetyNet : ISafetyNet
    {
        public async UniTask<Result> ExecuteAsync(HttpMethod method, string url)
        {
            await using var scope = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();

            bool isUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                         && (uriResult!.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (isUrl == false)
                return Result.ErrorResult($"CURL string is not url: {url}");

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

            return Result.ErrorResult($"JUANI ERROR CURL output: {output}; error: {errors}");
        }
    }
}
