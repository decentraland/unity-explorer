using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utility;
using Sentry;
using Sentry.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics.Sentry
{
    public class SentryReportHandler : ReportHandlerBase
    {
        public delegate void ConfigureScope(Scope scope);

        private static readonly TimeSpan SESSION_FLUSH_TIMEOUT = TimeSpan.FromSeconds(2);
        private const string UNKNOWN_SCENE_NAME = "unknown-scene";
        private const string CATEGORY_TAG = "category";

        // Native engine messages are captured by the Sentry Unity SDK's own log integration and never pass through ReportHub,
        // so they can only be filtered on the SDK's BeforeSend gateway. All of them are un-actionable for the client:
        // PhysX mesh-cooking warnings from creator assets (#7928, #10148, #10181), libcurl transport failures (#10182)
        // and camera projection warnings of off-screen points (#10154)
        private static readonly string[] IGNORED_NATIVE_MESSAGE_PREFIXES =
        {
            "[Physics.PhysX]",
            "Curl error ",
            "Screen position out of view frustum",
        };

        // Errors raised by scene code inside the ClearScript engine, matched by name so this assembly does not reference the engine (#10088, #10125)
        private const string SCRIPT_ENGINE_EXCEPTION_TYPE = "Microsoft.ClearScript.ScriptEngineException";

        // Connect failures of scene-owned websockets are the scene's problem; the same wrapper type is kept for the client's own sockets (#10103)
        private const string SCENE_WEBSOCKET_EXCEPTION_TYPE = "Utility.Networking.WebSocketException";

        // Thrown by the runtime's task machinery inside third-party transports (Sentry SDK, System.Net.Http) with no client frame (#10094, #10120, #10121, #10153)
        private const string TASK_ALREADY_COMPLETED_MESSAGE = "An attempt was made to transition a task to a final state when it had already completed.";

        // The user's disk is full: an environment condition, not a client defect (#10086). Matched by HRESULT first
        // (HRESULT_FROM_WIN32 of ERROR_HANDLE_DISK_FULL and ERROR_DISK_FULL, the codes the runtime attaches on every platform)
        // because the message text is platform-specific: Mono's own "Disk full. Path ..." on Unix, the OS-localized text on Windows
        private const int HANDLE_DISK_FULL_HRESULT = unchecked((int)0x80070027);
        private const int DISK_FULL_HRESULT = unchecked((int)0x80070070);
        private const string DISK_FULL_MESSAGE_PREFIX = "Disk full";

#if UNITY_EDITOR
        private const string EDITOR_DSN_ENV_VAR = "DCL_SENTRY_DSN";
#endif

        private static IReadOnlyList<ConfigureScope>? globalScopeConfigurators;

        private readonly List<ConfigureScope> scopeConfigurators = new (10);

        private readonly PerReportScope.Pool scopesPool;

        public SentryReportHandler(ICategorySeverityMatrix matrix, SentrySampler sentrySampler, bool debounceEnabled, string? sessionId)
            : base(ReportHandler.Sentry, matrix, debounceEnabled)
        {
            scopesPool = new PerReportScope.Pool(scopeConfigurators);
            globalScopeConfigurators = scopeConfigurators;

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

            // The single gateway every event passes through, whether it was captured via ReportHub or directly by the SDK's integrations
            options.SetBeforeSend(BeforeSend);

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

            // On the SDK's own scope so every event carries it, including those captured by the SDK's integrations and native crashes
            if (sessionId is { Length: > 0 } launcherSessionId)
                SentrySdk.ConfigureScope(scope => scope.SetTag("session_id", launcherSessionId));

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

        public static void ApplyGlobalScope(Scope scope)
        {
            IReadOnlyList<ConfigureScope>? configurators = globalScopeConfigurators;

            if (configurators == null)
                return;

            for (var i = 0; i < configurators.Count; i++)
            {
                try { configurators[i](scope); }
                catch (Exception) { /* ignored */ }
            }
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

        /// <summary>
        ///     Drops the events the client cannot act on, keeping them as breadcrumbs of the next real report,
        ///     and tags the remaining ones with a category so Sentry can always filter by it.
        /// </summary>
        internal static SentryEvent? BeforeSend(SentryEvent @event)
        {
            string? category = @event.Tags.TryGetValue(CATEGORY_TAG, out string? tag) ? tag : null;

            string? message = @event.Message?.Formatted ?? @event.Message?.Message;

            if (message != null && IsIgnoredNativeMessage(message))
                return Demote(message);

            if (@event.Exception != null && IsIgnoredException(@event.Exception, category))
                return Demote(@event.Exception.Message);

            if (category == null)
                @event.SetTag(CATEGORY_TAG, ReportCategory.UNSPECIFIED);

            return @event;
        }

        private static SentryEvent? Demote(string message)
        {
            SentrySdk.AddBreadcrumb(message, level: BreadcrumbLevel.Warning);
            return null;
        }

        private static bool IsIgnoredNativeMessage(string message)
        {
            for (var i = 0; i < IGNORED_NATIVE_MESSAGE_PREFIXES.Length; i++)
                if (message.StartsWith(IGNORED_NATIVE_MESSAGE_PREFIXES[i], StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static bool IsIgnoredException(Exception exception, string? category)
        {
            switch (exception)
            {
                // An aggregate (e.g. SceneExecutionException, an unobserved task) is ignored only when every inner exception is
                case AggregateException aggregate:
                    if (aggregate.InnerExceptions.Count == 0)
                        return false;

                    for (var i = 0; i < aggregate.InnerExceptions.Count; i++)
                        if (!IsIgnoredException(aggregate.InnerExceptions[i], category))
                            return false;

                    return true;

                case IOException:
                    return exception.HResult is HANDLE_DISK_FULL_HRESULT or DISK_FULL_HRESULT
                           || exception.Message.StartsWith(DISK_FULL_MESSAGE_PREFIX, StringComparison.Ordinal);

                case InvalidOperationException:
                    return exception.Message.StartsWith(TASK_ALREADY_COMPLETED_MESSAGE, StringComparison.Ordinal);
            }

            string? typeName = exception.GetType().FullName;

            if (typeName == SCRIPT_ENGINE_EXCEPTION_TYPE)
                return true;

            if (typeName == SCENE_WEBSOCKET_EXCEPTION_TYPE)
                return category == ReportCategory.JAVASCRIPT;

            return exception.InnerException != null && IsIgnoredException(exception.InnerException, category);
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

            // Exception.Message is virtual and may build its string lazily, so reports filtered out above never pay for it
            string message = exception.Message;

            if (string.IsNullOrEmpty(message))
                return;

            // default(SceneShortInfo) has a null Name
            string sceneName = data.SceneShortInfo.Name;
            scope.SetFingerprint("scene-js", string.IsNullOrEmpty(sceneName) ? UNKNOWN_SCENE_NAME : sceneName, FirstLine(message));
        }

        private static string FirstLine(string message)
        {
            int end = message.IndexOf('\n');

            if (end < 0)
                return message;

            // Excluding the '\r' of a "\r\n" ending here spares the extra string a TrimEnd would allocate
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
