using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using Utility;

namespace ECS.StreamableLoading.Common.Systems
{
    public static class AssetsLoadingUtility
    {
        public delegate UniTask<StreamableLoadingResult<TAsset>> InternalFlowDelegate<TAsset, in TState, in TIntention>(TIntention intention, TState state, IPartitionComponent partition, CancellationToken ct)
            where TIntention: struct, ILoadingIntention
            where TState: class;

        /// <summary>
        ///     Repeat the internal flow until attempts do not exceed or an irrecoverable error occurs
        /// </summary>
        /// <returns>
        ///     <para>Null - if PermittedSources have value</para>
        /// </returns>
        public static async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoopAsync<TIntention, TState, TAsset>(this TIntention intention,
            TState state,
            IPartitionComponent partition,
            InternalFlowDelegate<TAsset, TState, TIntention> flow, ReportData reportData, CancellationToken ct)
            where TIntention: struct, ILoadingIntention where TState: class
        {
            int attemptCount = intention.CommonArguments.Attempts;

            while (true)
            {
                ReportHub.Log(reportData, $"Starting loading {intention}\nfrom source {intention.CommonArguments.CurrentSource}\n{partition}, attempts left: {attemptCount}");

                try { return await flow(intention, state, partition, ct); }
                catch (UnityWebRequestException unityWebRequestException)
                {
                    // we can't access web request here as it is disposed already

                    // Decide if we can repeat or not
                    --attemptCount;

                    if (attemptCount <= 0 || unityWebRequestException.IsIrrecoverableError())
                    {
                        // no more sources left
                        ReportHub.Log(
                            reportData,
                            $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()} from source {intention.CommonArguments.CurrentSource}.\n"
                            + $"Trying sources: {intention.CommonArguments.PermittedSources} attemptCount {attemptCount} url: {intention.CommonArguments.URL}"
                        );

                        // Removal from Permitted Sources is done after this method
                        if (intention.CommonArguments.PermittedSources.HasExactlyOneFlag())

                            // conclude now
                            return new StreamableLoadingResult<TAsset>(
                                reportData,
                                new Exception(
                                    $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()} with url {intention.CommonArguments.URL}.\n"
                                    + "No more sources left.",
                                    unityWebRequestException
                                )
                            );

                        // Leave other systems to decide on other sources
                        return null;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException && e.InnerException is not OperationCanceledException)
                {
                    // General exception
                    // conclude now, we can't do anything
                    return new StreamableLoadingResult<TAsset>(reportData.WithStaticDebounce(), e);
                }
            }
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
