using DCL.Diagnostics;
using NUnit.Framework;
using Plugins.RustSegment.SegmentServerWrap;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utility.Multithreading;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     The native send daemon retries "(will retry)" send-loop failures without consuming the
    ///     spooled item, so the FFI error bridge reports them as warnings; every other native error
    ///     stays exception-level because it can mean lost events (instant-track "Network error" drops
    ///     the event without spooling it; a failed flush drops the already-extracted batch). The
    ///     operation-callback channel is natively always paired with the descriptive error message,
    ///     so it must not double-report at exception level.
    ///     These tests run on the matrix-bypassing DefaultReportLogger, which cannot observe
    ///     production filtering — the shipped ReportsHandlingSettings matrices must enable
    ///     (ANALYTICS, Warning) for the downgrade to stay visible, pinned by a dedicated test below.
    ///     The same logger applies no debouncing either, so the volume bound on the ~5/sec retry
    ///     flood is pinned directly against the report site's debouncer.
    /// </summary>
    public class RustSegmentErrorReportingShould
    {
        private const string PRODUCTION_SETTINGS_PATH = "Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportsHandlingSettingsProduction.asset";
        private const string DEVELOPMENT_SETTINGS_PATH = "Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportsHandlingSettingsDevelopment.asset";

        private const BindingFlags PRIVATE_STATIC = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PRIVATE_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Type SERVICE_TYPE = typeof(RustSegmentAnalyticsService);

        [SetUp]
        public void ResetStaticState()
        {
            SetCurrentInstance(null);
        }

        [TearDown]
        public void ClearCurrentInstance()
        {
            SetCurrentInstance(null);
        }

        [Test]
        public void ReportEachDistinctSendLoopRetryAsWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("item_id: 1"));
            LogAssert.Expect(LogType.Warning, new Regex("item_id: 2"));

            InvokeErrorCallback("Error executing send loop (will retry): ClientError { message: \"error sending request\", item_id: 1 }");
            InvokeErrorCallback("Error executing send loop (will retry): ClientError { message: \"error sending request\", item_id: 2 }");
        }

        [Test]
        public void CarryDebouncerCoveringBothProductionSinksOnRetryReport()
        {
            ReportData retryReport = GetRetryReport();

            Assert.That(retryReport.Debounce.Debouncer, Is.Not.Null,
                "the (will retry) error re-emits at the native daemon cadence (~5/sec) for the whole outage, so its report site must carry a debouncer");

            Assert.That(retryReport.Debounce.Debouncer!.AppliedTo, Is.EqualTo(ReportHandler.All),
                "the retry flood hits both the player log and Sentry breadcrumbs, so the debouncer must apply to both handlers");
        }

        [Test]
        public void DebounceRepeatedIdenticalRetryMessages()
        {
            IReportsDebouncer debouncer = GetRetryReport().Debounce.Debouncer!;

            // Unique per run: the production debouncer is static state that outlives a reused test domain
            var fingerprint = new ReportMessageFingerprint(
                $"Segment error: Error executing send loop (will retry): ClientError {{ item_id: {Guid.NewGuid():N} }}");

            Assert.That(debouncer.Debounce(fingerprint), Is.False, "the outage onset must stay visible");

            var anyDebounced = false;

            for (var i = 0; i < 16; i++)
                anyDebounced |= debouncer.Debounce(fingerprint);

            Assert.That(anyDebounced, Is.True, "repeated identical retry errors must be volume-bounded");
        }

        [Test]
        public void KeepInstantTrackNetworkErrorAsException()
        {
            // instant_track_and_flush drops the event on send failure — a real loss signal
            LogAssert.Expect(LogType.Exception, new Regex("Network error"));

            InvokeErrorCallback("Operation 278 failed: Network error");
        }

        [Test]
        public void KeepSqliteLockedFlushAsException()
        {
            // QueuedBatcher::flush extracts the batch before enque — a locked enque drops it
            LogAssert.Expect(LogType.Exception, new Regex("database is locked"));

            InvokeErrorCallback("Operation 2 failed: Cannot flush: sqlite error: database is locked");
        }

        [Test]
        public void KeepSendLoopDropAsException()
        {
            LogAssert.Expect(LogType.Exception, new Regex("will drop"));

            InvokeErrorCallback("Error executing send loop (will drop): QueueError(\"corrupt item\")");
        }

        [Test]
        public void KeepNonTransientErrorsAsExceptions()
        {
            LogAssert.Expect(LogType.Exception, new Regex("message too large"));

            InvokeErrorCallback("Operation 3 failed: Cannot enqueue: message too large");
        }

        [Test]
        public void ReportFailedOperationCallbackAsWarning()
        {
            RustSegmentAnalyticsService service = CreateServiceWithPendingOperation(5UL, out object responseError);
            SetCurrentInstance(service);

            LogAssert.Expect(LogType.Warning, new Regex("Segment operation 5 Flush failed with: Error"));

            MethodInfo callback = SERVICE_TYPE.GetMethod("Callback", PRIVATE_STATIC)!;
            callback.Invoke(null, new object[] { 5UL, responseError });
        }

        [Test]
        public void EnableAnalyticsWarningsInShippedReportMatrices()
        {
            var production = AssetDatabase.LoadAssetAtPath<ReportsHandlingSettings>(PRODUCTION_SETTINGS_PATH)!;
            var development = AssetDatabase.LoadAssetAtPath<ReportsHandlingSettings>(DEVELOPMENT_SETTINGS_PATH)!;

            Assert.That(production.GetMatrix(ReportHandler.Sentry).IsEnabled(ReportCategory.ANALYTICS, LogType.Warning), Is.True,
                "downgraded Segment transport warnings must reach Sentry breadcrumbs in production");

            Assert.That(production.GetMatrix(ReportHandler.DebugLog).IsEnabled(ReportCategory.ANALYTICS, LogType.Warning), Is.True,
                "downgraded Segment transport warnings must reach the production player log");

            Assert.That(development.GetMatrix(ReportHandler.DebugLog).IsEnabled(ReportCategory.ANALYTICS, LogType.Warning), Is.True,
                "downgraded Segment transport warnings must reach the development player log");
        }

        private static void InvokeErrorCallback(string message)
        {
            MethodInfo errorCallback = SERVICE_TYPE.GetMethod("ErrorCallback", PRIVATE_STATIC)!;
            IntPtr ptr = Marshal.StringToCoTaskMemUTF8(message);

            try { errorCallback.Invoke(null, new object[] { ptr }); }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }

        private static ReportData GetRetryReport()
        {
            FieldInfo field = SERVICE_TYPE.GetField("RETRY_REPORT", PRIVATE_STATIC)!;
            return (ReportData)field.GetValue(null)!;
        }

        private static void SetCurrentInstance(RustSegmentAnalyticsService? service)
        {
            FieldInfo currentField = SERVICE_TYPE.GetField("CURRENT", PRIVATE_STATIC)!;
            var current = (Mutex<RustSegmentAnalyticsService?>)currentField.GetValue(null)!;
            using Mutex<RustSegmentAnalyticsService?>.Guard guard = current.Lock();
            guard.Value = service;
        }

        private static RustSegmentAnalyticsService CreateServiceWithPendingOperation(ulong operationId, out object responseError)
        {
            var service = (RustSegmentAnalyticsService)FormatterServices.GetUninitializedObject(SERVICE_TYPE);

            FieldInfo afterCleanField = SERVICE_TYPE.GetField("afterClean", PRIVATE_INSTANCE)!;
            object afterClean = Activator.CreateInstance(afterCleanField.FieldType)!;

            Type tupleType = afterCleanField.FieldType.GetGenericArguments()[1];
            Type operationType = tupleType.GetGenericArguments()[0];
            Type listType = tupleType.GetGenericArguments()[1];
            object tuple = Activator.CreateInstance(tupleType, Enum.Parse(operationType, "Flush"), Activator.CreateInstance(listType))!;

            afterCleanField.FieldType.GetProperty("Item")!.SetValue(afterClean, tuple, new object[] { operationId });
            afterCleanField.SetValue(service, afterClean);

            Type responseType = typeof(NativeMethods).GetNestedType("Response", BindingFlags.NonPublic)!;
            responseError = Enum.Parse(responseType, "Error");

            return service;
        }
    }
}
