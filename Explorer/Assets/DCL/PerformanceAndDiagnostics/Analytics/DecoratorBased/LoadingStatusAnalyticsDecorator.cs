using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Web3.Identities;
using Segment.Serialization;
using Sentry;
using System;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class LoadingStatusAnalyticsDecorator : ILoadingStatus
    {
        private readonly ILoadingStatus core;
        private const string STAGE_KEY = "state";
        private const string STAGE_NAME = "stage_name";
        private int loadingScreenStageId;

        private readonly IAnalyticsController analytics;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private const string LOADING_TRANSACTION_NAME = "loading_process";

        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage => core.CurrentStage;
        public ReactiveProperty<string> AssetState => core.AssetState;
        private bool isFirstLoading;

        public LoadingStatusAnalyticsDecorator(ILoadingStatus loadingStatus, IAnalyticsController analytics, IWeb3IdentityCache web3IdentityCache)
        {
            core = loadingStatus;
            isFirstLoading = true;
            this.analytics = analytics;
            this.web3IdentityCache = web3IdentityCache;
            this.sentryTransactionManager = SentryTransactionManager.Instance;;
        }

        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            analytics.Track(AnalyticsEvents.General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"7.{(byte)stage} - loading screen: {stage.ToString()}" },
            });

            TrackLoadingStageWithSentry(stage);
        }

        private void TrackLoadingStageWithSentry(LoadingStatus.LoadingStage stage)
        {
            if (stage == LoadingStatus.LoadingStage.Init)
            {
                var transactionData = new TransactionData
                {
                    TransactionName = LOADING_TRANSACTION_NAME,
                    TransactionOperation = "loading",
                    Tags = web3IdentityCache.Identity == null
                        ? new[] { new KeyValuePair<string, string>("has_identity", "false") }
                        : new[] { new KeyValuePair<string, string>("has_identity", "true"), new KeyValuePair<string, string>("identity_expired", web3IdentityCache.Identity.IsExpired.ToString()) },
                };
                sentryTransactionManager.StartSentryTransaction(transactionData);
            }

            if (stage != LoadingStatus.LoadingStage.Completed)
            {
                var spanData = new SpanData
                {
                    TransactionName = LOADING_TRANSACTION_NAME,
                    SpanName = stage.ToString(),
                    SpanOperation = $"loading_stage_{stage.ToString().ToLower()}",
                    Depth = 0
                };
                sentryTransactionManager.StartSpan(spanData);
            }

            if (stage == LoadingStatus.LoadingStage.Completed)
            {
                var spanData = new SpanData
                {
                    TransactionName = LOADING_TRANSACTION_NAME,
                    SpanName = stage.ToString(),
                    SpanOperation = "loading_completed",
                    Depth = 0
                };
                sentryTransactionManager.StartSpan(spanData);
                sentryTransactionManager.EndTransaction(LOADING_TRANSACTION_NAME);
            }
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            core.UpdateAssetsLoaded(assetsLoaded, assetsToLoad);
        }

        public float SetCurrentStage(LoadingStatus.LoadingStage stage)
        {
            //After the first loading screen flow, we dont want to report analytics anymore
            if (isFirstLoading)
            {
                OnLoadingStageChanged(stage);
                isFirstLoading = stage != LoadingStatus.LoadingStage.Completed;
            }
            return core.SetCurrentStage(stage);
        }

        public bool IsLoadingScreenOn() =>
            core.IsLoadingScreenOn();
    }
}
