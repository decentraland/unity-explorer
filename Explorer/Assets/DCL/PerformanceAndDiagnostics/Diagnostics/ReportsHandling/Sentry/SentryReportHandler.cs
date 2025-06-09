using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using Sentry;
using Sentry.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics.Sentry
{
    public class SentryReportHandler : ReportHandlerBase
    {
        public delegate void ConfigureScope(Scope scope);

        private readonly List<ConfigureScope> scopeConfigurators = new (10);

        private readonly PerReportScope.Pool scopesPool;

        public SentryReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled)
            : base(matrix, debounceEnabled)
        {
            scopesPool = new PerReportScope.Pool(scopeConfigurators);

            // To prevent unwanted logs, manual initialization is required.
            // We need to delay the replacement of Debug.unityLogger.logHandler instance
            // to ensure that Unity's default logger is initially injected in our custom loggers.
            // After this initialization, Debug.unityLogger.logHandler is replaced which reports all the unhandled exceptions.
            // For this to work correctly, the "enabled" option in Assets/Resources/Sentry/SentryOptions.asset should be set to off
            // preventing `SentryInitialization` from running the app's startup process.
            var sentryUnityInfo = new SentryUnityInfo();
            SentryUnityOptions options = ScriptableSentryUnityOptions.LoadSentryUnityOptions(sentryUnityInfo);
            options!.Enabled = true;

            if (!IsValidConfiguration(options))
            {
                Debug.LogWarning($"Cannot initialize Sentry due invalid configuration: {options.Dsn}");
                return;
            }

            SentrySdk.Init(options);
        }

        public void AddIdentityToScope(Scope scope, string wallet)
        {
            scope.SetTag("wallet", wallet);
        }

        public void AddCurrentSceneToScope(Scope scope, SceneShortInfo sceneInfo)
        {
            scope.SetTag("current_scene.base_parcel", sceneInfo.BaseParcel.ToString());
            scope.SetTag("current_scene.name", sceneInfo.Name);
        }

        public void AddRealmInfoToScope(Scope scope, string baseCatalystUrl, string baseContentUrl, string baseLambdaUrl)
        {
            scope.SetTag("base_catalyst_url", baseCatalystUrl);
            scope.SetTag("base_content_url", baseContentUrl);
            scope.SetTag("base_lambda_url", baseLambdaUrl);
        }

        public void AddScopeConfigurator(ConfigureScope configureScope)
        {
            scopeConfigurators.Add(configureScope);
        }

        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            CaptureMessage(message.ToString(), category, logType);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            var format = string.Format(message.ToString(), args);
            CaptureMessage(format, category, logType);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            SentrySdk.CaptureException(ecsSystemException);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            using PoolExtensions.Scope<PerReportScope> reportScope = scopesPool.Scope(reportData);
            SentrySdk.CaptureException(exception, reportScope.Value.ExecuteCached);
        }

        internal override void HandleSupressedException(Exception exception, ReportData reportData)
        {
            //Add breadcrumb for non AB categories. AB categories will flood our Sentry without meaningfull information
            if (reportData.Category.Equals(ReportCategory.ASSET_BUNDLES))
                return;

            SentrySdk.AddBreadcrumb(
                $"Suppressed exception {reportData.Category}: {exception.Message}");
        }

        private void CaptureMessage(string message, ReportData reportData, LogType logType)
        {
            // Avoid reporting non-errors to sentry as separate issues (even if they are enabled in the matrix)
            // Report them as breadcrumbs instead

            SentryLevel sentryLevel = ToSentryLevel(logType);

            switch (sentryLevel)
            {
                case SentryLevel.Info:
                    SentrySdk.AddBreadcrumb(message, reportData.Category, level: BreadcrumbLevel.Info);
                    break;
                case SentryLevel.Debug:
                    SentrySdk.AddBreadcrumb(message, reportData.Category, level: BreadcrumbLevel.Debug);
                    break;
                case SentryLevel.Warning:
                    SentrySdk.AddBreadcrumb(message, reportData.Category, level: BreadcrumbLevel.Warning);
                    break;
                default:
                {
                    using PoolExtensions.Scope<PerReportScope> reportScope = scopesPool.Scope(reportData);
                    SentrySdk.CaptureMessage(message, reportScope.Value.ExecuteCached, sentryLevel);
                }

                    break;
            }
        }

        private bool IsValidConfiguration(SentryUnityOptions options) =>
            !string.IsNullOrEmpty(options.Dsn)
            && options.Dsn != "<REPLACE_DSN>";

        private static SentryLevel ToSentryLevel(in LogType logType)
        {
            switch (logType)
            {
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    return SentryLevel.Error;
                case LogType.Log:
                default:
                    return SentryLevel.Info;
                case LogType.Warning:
                    return SentryLevel.Warning;
            }
        }

        private class PerReportScope
        {
            public readonly Action<Scope> ExecuteCached;

            private readonly IReadOnlyList<ConfigureScope> scopeConfigurators;

            internal ReportData reportData { private get; set; }

            private PerReportScope(IReadOnlyList<ConfigureScope> scopeConfigurators)
            {
                this.scopeConfigurators = scopeConfigurators;

                ExecuteCached = Execute;
            }

            private void Execute(Scope scope)
            {
                // Add global scope

                for (var i = 0; i < scopeConfigurators.Count; i++)
                    scopeConfigurators[i](scope);

                // Add local scope

                AddCategoryTag(scope, reportData);
                AddSceneInfo(scope, reportData);
            }

            private static void AddCategoryTag(Scope scope, ReportData data) =>
                scope.SetTag("category", data.Category);

            private static void AddSceneInfo(Scope scope, ReportData data)
            {
                if (data.SceneShortInfo.BaseParcel != Vector2Int.zero) ;
                scope.SetTag("scene.base_parcel", data.SceneShortInfo.BaseParcel.ToString());

                scope.SetTag("scene.name", data.SceneShortInfo.Name);
            }

            internal class Pool : ThreadSafeObjectPool<PerReportScope>
            {
                public Pool(IReadOnlyList<ConfigureScope> scopeConfigurators) : base(
                    () => new PerReportScope(scopeConfigurators), defaultCapacity: 3, collectionCheck: PoolConstants.CHECK_COLLECTIONS) { }

                public PoolExtensions.Scope<PerReportScope> Scope(ReportData reportData)
                {
                    PoolExtensions.Scope<PerReportScope> scope = this.AutoScope();
                    scope.Value.reportData = reportData;
                    return scope;
                }
            }
        }
    }
}
