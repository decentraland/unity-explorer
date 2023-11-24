using Sentry;
using Sentry.Unity;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics.Sentry
{
    public class SentryReportHandler : ReportHandlerBase
    {
        public SentryReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled)
            : base(matrix, debounceEnabled)
        {
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

        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            SentrySdk.CaptureMessage(message.ToString(), scope => AddReportData(scope, in category), ToSentryLevel(in logType));
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            var format = string.Format(message.ToString(), args);
            SentrySdk.CaptureMessage(format, scope => AddReportData(scope, in category), ToSentryLevel(in logType));
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            SentrySdk.CaptureException(ecsSystemException);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            SentrySdk.CaptureException(exception, scope => AddReportData(scope, in reportData));
        }

        private void AddReportData(Scope scope, in ReportData data)
        {
            AddCategoryTag(scope, in data);
            AddSceneInfo(scope, in data);
        }

        private void AddCategoryTag(Scope scope, in ReportData data) =>
            scope.SetTag("category", data.Category);

        private void AddSceneInfo(Scope scope, in ReportData data)
        {
            if (data.SceneShortInfo.BaseParcel == Vector2Int.zero) return;
            scope.SetTag("scene.base_parcel", data.SceneShortInfo.BaseParcel.ToString());
            scope.SetTag("scene.name", data.SceneShortInfo.Name);
        }

        private bool IsValidConfiguration(SentryUnityOptions options) =>
            !string.IsNullOrEmpty(options.Dsn)
            && options.Dsn != "<REPLACE_DSN>";

        private SentryLevel ToSentryLevel(in LogType logType)
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
    }
}
