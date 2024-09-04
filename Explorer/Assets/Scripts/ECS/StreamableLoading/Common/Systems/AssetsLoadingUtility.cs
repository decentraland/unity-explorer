using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Common.Systems
{
    public static class AssetsLoadingUtility
    {
        public delegate UniTask<StreamableLoadingResult<TAsset>> InternalFlowDelegate<TAsset, in TIntention>(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            where TIntention: struct, ILoadingIntention;

        /// <summary>
        ///     Repeat the internal flow until attempts do not exceed or an irrecoverable error occurs
        /// </summary>
        /// <returns>
        ///     <para>Null - if PermittedSources have value</para>
        /// </returns>
        public static async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoopAsync<TIntention, TAsset>(this TIntention intention,
            IAcquiredBudget acquiredBudget,
            IPartitionComponent partition,
            InternalFlowDelegate<TAsset, TIntention> flow, ReportData reportData, CancellationToken ct)
            where TIntention: struct, ILoadingIntention
        {
            int attemptCount = intention.CommonArguments.Attempts;

            while (true)
            {
                ReportHub.Log(reportData, $"Starting loading {intention}\n{partition}, attempts left: {attemptCount}");

                try { return await flow(intention, acquiredBudget, partition, ct); }
                catch (UnityWebRequestException unityWebRequestException)
                {
                    // we can't access web request here as it is disposed already

                    // no more sources left
                    if (intention.CommonArguments.PermittedSources == AssetSource.NONE)
                    {
                        ReportHub.LogError(reportData, $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()} with url {intention.CommonArguments.URL}.\n"
                                                       + "No more sources left.");

                        ReportHub.LogException(unityWebRequestException, reportData);
                    }
                    else
                    {
                        ReportHub.Log(reportData, $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()}.\n"
                                                  + $"Trying sources: {intention.CommonArguments.PermittedSources} attemptCount {attemptCount} url: {intention.CommonArguments.URL}");
                    }

                    // Decide if we can repeat or not
                    --attemptCount;

                    if (unityWebRequestException.IsIrrecoverableError(attemptCount))
                    {
                        if (intention.CommonArguments.PermittedSources == AssetSource.NONE)

                            // conclude now
                            return new StreamableLoadingResult<TAsset>(unityWebRequestException);

                        // Leave other systems to decide on other sources
                        return null;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException && e.InnerException is not OperationCanceledException)
                {
                    // General exception
                    // conclude now, we can't do anything
                    ReportException(reportData, e);
                    return new StreamableLoadingResult<TAsset>(e);
                }
            }
        }

        public static void ReportException(ReportData reportData, Exception exception)
        {
            ReportHub.LogException(exception, new ReportData(reportData.Category, ReportHint.SessionStatic | reportData.Hint, reportData.SceneShortInfo));
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
