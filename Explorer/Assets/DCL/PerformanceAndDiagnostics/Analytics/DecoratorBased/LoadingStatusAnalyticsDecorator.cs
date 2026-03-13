using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class LoadingStatusAnalyticsDecorator : ILoadingStatus
    {
        private const string STAGE_KEY = "state";

        private const string LOADING_TRANSACTION_NAME = "loading_process";
        private readonly ILoadingStatus core;

        private readonly IAnalyticsController analytics;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private int loadingScreenStageId;
        private bool isFirstLoading;

        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage => core.CurrentStage;
        public ReactiveProperty<string> AssetState => core.AssetState;

        public LoadingStatusAnalyticsDecorator(ILoadingStatus loadingStatus, IAnalyticsController analytics, IWeb3IdentityCache web3IdentityCache)
        {
            core = loadingStatus;
            isFirstLoading = true;
            this.analytics = analytics;
            this.web3IdentityCache = web3IdentityCache;
        }

        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            analytics.Track(AnalyticsEvents.General.INITIAL_LOADING, new JObject
            {
                { STAGE_KEY, $"7.{(byte)stage} - loading screen: {stage.ToString()}" },
            });

            TrackLoadingStageWithSentry(stage);
        }

        private void TrackLoadingStageWithSentry(LoadingStatus.LoadingStage stage)
        {
            if (stage == LoadingStatus.LoadingStage.Init)
            {
                var tags = new List<KeyValuePair<string, string>>(3)
                {
                    new ("launch_count", LaunchCounter.Count.ToString()),
                };

                if (web3IdentityCache.Identity == null)
                    tags.Add(new KeyValuePair<string, string>("has_identity", "false"));
                else
                {
                    tags.Add(new KeyValuePair<string, string>("has_identity", "true"));
                    tags.Add(new KeyValuePair<string, string>("identity_expired", web3IdentityCache.Identity.IsExpired.ToString()));
                }

                var transactionData = new TransactionData
                {
                    TransactionName = LOADING_TRANSACTION_NAME,
                    TransactionOperation = "loading",
                    Tags = tags,
                };

                SentryTransactionNameMapping.Instance.StartSentryTransaction(transactionData);
            }

            if (stage != LoadingStatus.LoadingStage.Completed)
            {
                var spanData = new SpanData
                {
                    SpanName = stage.ToString(),
                    SpanOperation = $"loading_stage_{stage.ToString().ToLower()}",
                    Depth = 0,
                };

                SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, spanData);
            }

            if (stage == LoadingStatus.LoadingStage.Completed)
            {
                var spanData = new SpanData
                {
                    SpanName = stage.ToString(),
                    SpanOperation = "loading_completed",
                    Depth = 0,
                };

                SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, spanData);
                SentryTransactionNameMapping.Instance.EndTransaction(LOADING_TRANSACTION_NAME);
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
