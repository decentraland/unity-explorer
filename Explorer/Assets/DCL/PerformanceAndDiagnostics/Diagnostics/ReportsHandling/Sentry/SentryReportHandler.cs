using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utility;
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

        private static readonly TimeSpan SESSION_FLUSH_TIMEOUT = TimeSpan.FromSeconds(2);
        private const string UNKNOWN_SCENE_NAME = "unknown-scene";

#if UNITY_EDITOR
        private const string EDITOR_DSN_ENV_VAR = "DCL_SENTRY_DSN";
#endif

        private readonly List<ConfigureScope> scopeConfigurators = new (10);

        private readonly PerReportScope.Pool scopesPool;

        public SentryReportHandler(ICategorySeverityMatrix matrix, SentrySampler sentrySampler, bool debounceEnabled)
            : base(ReportHandler.Sentry, matrix, debounceEnabled)
        {
            scopesPool = new PerReportScope.Pool(scopeConfigurators);

            // To prevent unwanted logs, manual initialization is required.
            // We need to delay the replacement of Debug.unityLogger.logHandler instance
            // to ensure that Unity's default logger is initially injected in our custom loggers.
            // After this initialization, Debug.unityLogger.logHandler is replaced which reports all the unhandled exceptions.
            // For this to work correctly, the "enabled" option in Assets/Resources/Sentry/SentryOptions.asset should be set to off
            // preventing `SentryInitialization` from running the app's startup process.
            SentryUnityOptions? options = ScriptableSentryUnityOptions.LoadSentryUnityOptions();

            if (options == null)
            {
#if !UNITY_EDITOR
                Debug.LogWarning($"Cannot initialize Sentry due non-existent configuration");
#endif

                return;
            }

            options.Enabled = true;
            options.TracesSampler = sentrySampler.Execute;

#if UNITY_EDITOR
            // The asset carries a placeholder DSN that only CI replaces, so editor sessions resolve one from the environment instead.
            if (!IsValidConfiguration(options))
            {
                string? editorDsn = Environment.GetEnvironmentVariable(EDITOR_DSN_ENV_VAR);

                if (!string.IsNullOrWhiteSpace(editorDsn))
                {
                    options.Dsn = editorDsn;
                    options.Environment = "editor";
                    options.CaptureInEditor = true;
                }
            }
#endif

            if (!IsValidConfiguration(options))
            {
#if !UNITY_EDITOR
                Debug.LogWarning($"Cannot initialize Sentry due invalid configuration: {options.Dsn}");
#endif
                return;
            }

            SentrySdk.Init(options);

            ExitUtils.RegisterCleanUpCandidate(new OnQuittingCleanUpCandidate(nameof(SentryReportHandler), EndSessionAndFlush));
        }

        private static void EndSessionAndFlush()
        {
            SentrySdk.EndSession();
            SentrySdk.Flush(SESSION_FLUSH_TIMEOUT);
        }

        public void AddMeetMinimumRequirements(Scope scope, bool meets)
        {
            scope.SetTag("meets_minimum_requirements", meets.ToString());
        }

        public void AddIdentityToScope(Scope scope, string wallet)
        {
            scope.SetTag("wallet", wallet);
        }

        public void AddCurrentSceneToScope(Scope scope, SceneShortInfo sceneInfo)
        {
            scope.SetTag("current_scene.base_parcel", sceneInfo.BaseParcel.ToString());
            scope.SetTag("current_scene.name", sceneInfo.Name);

            if (!string.IsNullOrEmpty(sceneInfo.SdkVersion))
                scope.SetTag("current_scene.sdk_version", sceneInfo.SdkVersion);
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

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object? context)
        {
            using PoolExtensions.Scope<PerReportScope> reportScope = scopesPool.Scope(reportData, exception);
            SentrySdk.CaptureException(exception, reportScope.Value.ExecuteCached);
        }

        internal override void HandleSuppressedException(Exception exception, ReportData reportData)
        {
            //Add breadcrumb for non AB categories. AB categories will flood our Sentry without meaningful information
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

        internal static void AddSceneJsFingerprint(Scope scope, in ReportData data, Exception? exception)
        {
            if (exception == null)
                return;

            if (!data.Category.Equals(ReportCategory.JAVASCRIPT))
                return;

            // Exception.Message is a virtual getter that may build its string lazily, so it is
            // only read after the category guard has passed
            string message = exception.Message;

            if (string.IsNullOrEmpty(message))
                return;

            scope.SetFingerprint("scene-js", data.SceneShortInfo.Name ?? UNKNOWN_SCENE_NAME, FirstLine(message));
        }

        private static string FirstLine(string message)
        {
            int end = message.IndexOf('\n');

            if (end < 0)
                return message;

            // Fold a trailing '\r' (from "\r\n") into the slice instead of a second TrimEnd allocation
            if (end > 0 && message[end - 1] == '\r')
                end--;

            return message.Substring(0, end);
        }

        private class PerReportScope
        {
            public readonly Action<Scope> ExecuteCached;

            private readonly IReadOnlyList<ConfigureScope> scopeConfigurators;

            private ReportData reportData { get; set; }
            private Exception? exception { get; set; }

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
                AddSceneJsFingerprint(scope, reportData, exception);
            }

            private static void AddCategoryTag(Scope scope, ReportData data) =>
                scope.SetTag("category", data.Category);

            private static void AddSceneInfo(Scope scope, ReportData data)
            {
                scope.SetTag("scene.base_parcel", data.SceneShortInfo.BaseParcel.ToString());
                scope.SetTag("scene.name", data.SceneShortInfo.Name);

                if (!string.IsNullOrEmpty(data.SceneShortInfo.SdkVersion))
                    scope.SetTag("scene.sdk_version", data.SceneShortInfo.SdkVersion);
            }

            internal class Pool : ThreadSafeObjectPool<PerReportScope>
            {
                public Pool(IReadOnlyList<ConfigureScope> scopeConfigurators) : base(
                    () => new PerReportScope(scopeConfigurators), defaultCapacity: 3, collectionCheck: PoolConstants.CHECK_COLLECTIONS) { }

                public PoolExtensions.Scope<PerReportScope> Scope(ReportData reportData, Exception? exception = null)
                {
                    PoolExtensions.Scope<PerReportScope> scope = this.AutoScope();
                    scope.Value.reportData = reportData;
                    scope.Value.exception = exception;
                    return scope;
                }
            }
        }
    }
}
