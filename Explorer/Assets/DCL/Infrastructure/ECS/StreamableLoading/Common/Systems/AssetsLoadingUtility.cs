using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
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
            InternalFlowDelegate<TAsset, TState, TIntention> flow,
            IDictionary<IntentionsComparer<TIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?>? irrecoverableFailures,
            IntentionsComparer<TIntention>.SourcedIntentionId intentionId,
            ReportData reportData,
            CancellationToken ct)
            where TIntention: struct, ILoadingIntention, IEquatable<TIntention> where TState: class
        {
            int attemptCount = intention.CommonArguments.Attempts;

            while (true)
            {
                ReportHub.Log(reportData, $"Starting loading {intention}\nfrom source {intention.CommonArguments.CurrentSource}\n{partition}, attempts left: {attemptCount}");

                try { return await flow(intention, state, partition, ct); }
                catch (WebRequestException webRequestException)
                {
                    // Decide if we can repeat or not
                    --attemptCount;

                    if (webRequestException.IsIrrecoverableError(attemptCount))
                    {
                        ReportHub.Log(
                            reportData,
                            $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()} from source {intention.CommonArguments.CurrentSource}.\n"
                            + $"Trying sources: {intention.CommonArguments.PermittedSources} attemptCount {attemptCount} url: {intention.CommonArguments.URL}"
                        );

                        // Removal from Permitted Sources is done after this method
                        if (intention.CommonArguments.PermittedSources.HasExactlyOneFlag())
                        {
                            var failure = new StreamableLoadingResult<TAsset>(
                                reportData,
                                new Exception(
                                    $"Exception occured on loading {typeof(TAsset)} from {intention.ToString()} with url {intention.CommonArguments.URL}.\n"
                                    + "No more sources left.",
                                    webRequestException
                                )
                            );

                            SetIrrecoverableFailure(failure);

                            // conclude now - no sources left
                            return failure;
                        }

                        // For this URL it's an irrecoverable failure
                        SetIrrecoverableFailure(null); // null means it can be continued - it's not the final result

                        // Leave other systems to decide on other sources
                        return null;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException && e.InnerException is not OperationCanceledException)
                {
                    ReportHub.Log(
                        reportData,
                        $"Non-Web Exception occured on loading {typeof(TAsset)} from {intention.ToString()} from source {intention.CommonArguments.CurrentSource}"
                    );

                    // General exception
                    // conclude now, we can't do anything
                    var failure = new StreamableLoadingResult<TAsset>(reportData.WithSessionStatic(), e);
                    SetIrrecoverableFailure(failure);

                    return failure;
                }
            }

            void SetIrrecoverableFailure(StreamableLoadingResult<TAsset>? failure)
            {
                if (irrecoverableFailures == null)
                    return;

                bool result = irrecoverableFailures.SyncTryAdd(intentionId, failure);
                if (result == false) ReportHub.LogError(reportData, $"Irrecoverable failure for {intention} is already added");
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
