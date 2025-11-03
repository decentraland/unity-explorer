// File: UwrPerf_Min.cs
// Requirements: Test asmdef referencing Unity.PerformanceTesting + UnityEngine.TestRunner

using System.Collections;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using NUnit.Framework;
using System;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

public class UwrPerf_Min
{
    private const string Url = "https://social-api.decentraland.org/v1/communities?search=&onlyMemberOf=true&offset=-100&limit=100"; // small, reliable

    private static IEnumerator Await(Task t)
    {
        while (!t.IsCompleted) yield return null;
        if (t.IsFaulted) ExceptionDispatchInfo.Capture(t.Exception.InnerException).Throw();
    }

    private static double Us(long a, long b) =>
        (b - a) * (1_000_000.0 / Stopwatch.Frequency);

    private static async Task<int> FetchAsync(string url)
    {
        // Minimal phases: Build → Wait → Read
        var req = UnityWebRequest.Get(url);
        UnityWebRequestAsyncOperation? op = req.SendWebRequest();
        await op; // await UnityWebRequestAsyncOperation

#if UNITY_2020_2_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success) throw new Exception(req.error);
#else
        if (req.isNetworkError || req.isHttpError) throw new System.Exception(req.error);
#endif
        int bytes = req.downloadHandler?.data?.Length ?? 0;
        req.Dispose();
        return bytes;
    }

    [UnityTest] [Performance]
    public IEnumerator UnityWebRequest_TotalLatency_Minimal()
    {
        var gTotal = new SampleGroup("UWR.Total", SampleUnit.Microsecond);
        var gBytes = new SampleGroup("UWR.ResponseBytes", SampleUnit.Byte);

        // (Optional) stabilize environment
        Application.targetFrameRate = 1000;
        QualitySettings.vSyncCount = 0;

        // Warmup a few times (DNS/TLS/JIT)
        for (int i = 0; i < 3; i++)
            yield return Await(FetchAsync(Url));

        const int measurements = 5;
        const int iters = 2;

        for (int m = 0; m < measurements; m++)
        {
            for (int i = 0; i < iters; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                int bytes = 0;

                Task<int> task = FetchAsync(Url);
                yield return Await(task);
                bytes = task.Result;

                long t1 = Stopwatch.GetTimestamp();

                Measure.Custom(gTotal, Us(t0, t1));
                Measure.Custom(gBytes, bytes);
            }

            yield return null; // small spacer frame between measurements
        }
    }
}
