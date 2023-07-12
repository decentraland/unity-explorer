using AssetManagement;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Common.Systems
{
    public static class AssetsLoadingUtility
    {
        public delegate UniTask<StreamableLoadingResult<TAsset>> InternalFlowDelegate<TAsset, in TIntention>(TIntention intention, CancellationToken ct)
            where TIntention: struct, ILoadingIntention;

        /// <summary>
        ///     Repeat the internal flow until attempts do not exceed or an irrecoverable error occurs
        /// </summary>
        /// <returns>
        ///     <para>Null - if PermittedSources have value</para>
        /// </returns>
        public static async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoop<TIntention, TAsset>(this TIntention intention,
            InternalFlowDelegate<TAsset, TIntention> flow, string reportCategory, CancellationToken ct)
            where TIntention: struct, ILoadingIntention
        {
            int attemptCount = intention.CommonArguments.Attempts;

            while (true)
            {
                try { return await flow(intention, ct); }

                catch (UnityWebRequestException unityWebRequestException)
                {
                    UnityWebRequest webRequest = unityWebRequestException.UnityWebRequest;

                    ReportHub.LogError(reportCategory, $"Exception occured on loading {typeof(TAsset)} from {webRequest.url}");
                    ReportHub.LogException(unityWebRequestException, reportCategory);

                    // Decide if we can repeat or not
                    --attemptCount;

                    bool isIrrecoverableError = !webRequest.IsServerError();

                    if (attemptCount <= 0 || webRequest.IsAborted() || isIrrecoverableError)
                    {
                        if (intention.CommonArguments.PermittedSources == AssetSource.NONE)

                            // conclude now
                            return new StreamableLoadingResult<TAsset>(unityWebRequestException);

                        // Leave other systems to decide on other sources
                        return null;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // General exception
                    // conclude now, we can't do anything
                    ReportException(reportCategory, e);
                    return new StreamableLoadingResult<TAsset>(e);
                }
            }
        }

        public static void ReportException(string category, Exception exception)
        {
            ReportHub.LogException(exception, new ReportData(category, ReportHint.SessionStatic));
        }

        public static StreamableLoadingResult<TAsset> Denullify<TAsset>(this in StreamableLoadingResult<TAsset>? loadingResult)
        {
            if (loadingResult == null)
                throw new ArgumentNullException(nameof(loadingResult));

            return loadingResult.Value;
        }

        /// <summary>
        ///     Throws and exception if the loading result is an exception or is null
        /// </summary>
        public static TAsset UnwrapAndRethrow<TAsset>(this in StreamableLoadingResult<TAsset>? loadingResult)
        {
            if (loadingResult == null)
                throw new ArgumentNullException(nameof(loadingResult));

            if (!loadingResult.Value.Succeeded)
                throw loadingResult.Value.Exception;

            return loadingResult.Value.Asset;
        }
    }
}
