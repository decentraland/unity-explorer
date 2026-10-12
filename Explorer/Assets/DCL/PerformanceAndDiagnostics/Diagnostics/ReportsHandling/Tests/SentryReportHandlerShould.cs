using DCL.Diagnostics.Sentry;
using NUnit.Framework;
using Sentry;
using System;
using System.IO;
using UnityEngine;
using Utility.Networking;

namespace DCL.Diagnostics.Tests
{
    public class SentryReportHandlerShould
    {
        private const string TASK_ALREADY_COMPLETED = "An attempt was made to transition a task to a final state when it had already completed.";

        private static readonly SceneShortInfo SCENE_INFO = new (Vector2Int.zero, "kingdom-of-antrom");

        private static Scope NewScope() =>
            new (new SentryOptions());

        [TestCase("[Physics.PhysX] cleaning the mesh failed")]
        [TestCase("Curl error 56: Recv failure: Connection reset by peer")]
        [TestCase("Screen position out of view frustum (screen pos 33.000000, 1860.000000) (Camera rect 0 0 3024 1832)")]
        public void DropUnactionableNativeMessages(string message)
        {
            var @event = new SentryEvent { Message = message };

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepOtherNativeMessages()
        {
            var @event = new SentryEvent { Message = "Shader error in 'Custom/Toon': undeclared identifier" };

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropDiskFullEvenWhenUnobservedTaskWrapsIt()
        {
            var @event = new SentryEvent(new AggregateException(new IOException("Disk full. Path /Users/jane/userdata_0.json")));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [TestCase(unchecked((int)0x80070027))]
        [TestCase(unchecked((int)0x80070070))]
        public void DropDiskFullByHResultWhateverTheMessageSays(int hresult)
        {
            var @event = new SentryEvent(new IOException("Win32 IO returned ERROR_DISK_FULL. Path C:\\Users\\jane\\userdata_0.json", hresult));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepOtherIoExceptions()
        {
            var @event = new SentryEvent(new IOException("Sharing violation on path C:\\Users\\jane\\userdata_0.json", unchecked((int)0x80070020)));

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropTaskAlreadyCompletedTransitions()
        {
            var @event = new SentryEvent(new InvalidOperationException(TASK_ALREADY_COMPLETED));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepAggregateWhenAnyInnerExceptionIsActionable()
        {
            var @event = new SentryEvent(new AggregateException(new IOException("Disk full"), new NullReferenceException()));

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropSceneWebSocketFailuresOnly()
        {
            var sceneEvent = new SentryEvent(new WebSocketException());
            sceneEvent.SetTag("category", ReportCategory.JAVASCRIPT);

            var clientEvent = new SentryEvent(new WebSocketException());
            clientEvent.SetTag("category", ReportCategory.LIVEKIT);

            Assert.IsNull(SentryReportHandler.BeforeSend(sceneEvent));
            Assert.IsNotNull(SentryReportHandler.BeforeSend(clientEvent));
        }

        [Test]
        public void TagEventsWithoutCategoryAsUnspecified()
        {
            var @event = new SentryEvent(new NullReferenceException());

            SentryEvent? sent = SentryReportHandler.BeforeSend(@event);

            Assert.IsNotNull(sent);
            Assert.AreEqual(ReportCategory.UNSPECIFIED, sent!.Tags["category"]);
        }

        [Test]
        public void KeepExistingCategoryTag()
        {
            var @event = new SentryEvent(new NullReferenceException());
            @event.SetTag("category", ReportCategory.ECS);

            SentryEvent? sent = SentryReportHandler.BeforeSend(@event);

            Assert.AreEqual(ReportCategory.ECS, sent!.Tags["category"]);
        }

        [Test]
        public void SetFingerprintForJavaScriptExceptionWithSceneAndMultilineMessage()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);
            var exception = new Exception("ReferenceError: applyPartMaterial is not defined\n    at childPart (Script [19]:65573:5)");

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, exception);

            CollectionAssert.AreEqual(new[] { "scene-js", "kingdom-of-antrom", "ReferenceError: applyPartMaterial is not defined" }, scope.Fingerprint);
        }

        [Test]
        public void TrimCarriageReturnFromFirstLineOfWindowsLineEndingMessage()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);
            var exception = new Exception("TypeError: Vector33.Create is not a function\r\n    at updateWarpBall (Script [19]:40371:51)");

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, exception);

            CollectionAssert.AreEqual(new[] { "scene-js", "kingdom-of-antrom", "TypeError: Vector33.Create is not a function" }, scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintWhenExceptionIsNull()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, null);

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintWhenExceptionMessageIsEmpty()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception(string.Empty));

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintForNonJavaScriptCategory()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.ENGINE, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception("Error: boom"));

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void SetFingerprintWithFallbackSceneNameWhenSceneShortInfoIsMissing()
        {
            Scope scope = NewScope();

            // Omitting sceneShortInfo leaves default(SceneShortInfo)'s null Name, the only state that triggers the fallback
            var reportData = new ReportData(ReportCategory.JAVASCRIPT);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception("Error: boom"));

            CollectionAssert.AreEqual(new[] { "scene-js", "unknown-scene", "Error: boom" }, scope.Fingerprint);
        }
    }
}
