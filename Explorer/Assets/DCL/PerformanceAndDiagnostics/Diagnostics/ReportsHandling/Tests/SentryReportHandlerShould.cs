using Cysharp.Threading.Tasks;
using DCL.Diagnostics.Sentry;
using NUnit.Framework;
using Sentry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Networking;

namespace DCL.Diagnostics.Tests
{
    public class SentryReportHandlerShould
    {
        private const string TASK_ALREADY_COMPLETED = "An attempt was made to transition a task to a final state when it had already completed.";

        private static readonly SceneShortInfo SCENE_INFO = new (Vector2Int.zero, "kingdom-of-antrom");

        private static Scope NewScope() =>
            new (new SentryOptions());

        // A real request to a loopback listener is the only way a UnityWebRequestException gets a response code.
        private static async Task<UnityWebRequestException> NewFailedRequestExceptionAsync(int statusCode)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            Task serverTask = ServeOneResponseAsync(listener, statusCode);

            try
            {
                using UnityWebRequest request = UnityWebRequest.Get($"http://127.0.0.1:{port}/places");
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);

                // EditMode tests do not run the player loop, so only a real-time delay is guaranteed to elapse.
                while (!operation.isDone && DateTime.UtcNow < deadline)
                    await Task.Delay(20);

                Assert.That(request.responseCode, Is.EqualTo(statusCode), "The loopback listener should have answered the request");

                return new UnityWebRequestException(request);
            }
            finally
            {
                listener.Stop();
                listener.Close();

                try { await serverTask; }
                catch (Exception)
                {
                    // A listener stopped before serving faults its pending GetContextAsync, which the assertion above already covers.
                }
            }
        }

        private static async Task ServeOneResponseAsync(HttpListener listener, int statusCode)
        {
            HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = statusCode;
            context.Response.Close();
        }

        private static IEnumerable<Exception> NetworkingFailures()
        {
            yield return new UnityWebRequestException(new UnityWebRequest("http://127.0.0.1:8767/hamsterwood/comms"));
            yield return new WebException("The remote server returned an error: (404) Not Found.");
            yield return new SocketException((int)SocketError.ConnectionRefused);
        }

        [TestCase("[Physics.PhysX] cleaning the mesh failed")]
        [TestCase("Curl error 56: Recv failure: Connection reset by peer")]
        [TestCase("Screen position out of view frustum (screen pos 33.000000, 1860.000000) (Camera rect 0 0 3024 1832)")]
        [TestCase("Platform does not support compute shaders")]
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
        public void DropInsecureConnectionRefusals()
        {
            var @event = new SentryEvent(new InvalidOperationException("Insecure connection not allowed"));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepOtherInvalidOperations()
        {
            var @event = new SentryEvent(new InvalidOperationException("Collection was modified"));

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepAggregateWhenAnyInnerExceptionIsActionable()
        {
            var @event = new SentryEvent(new AggregateException(new IOException("Disk full"), new NullReferenceException()));

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [TestCase(ReportCategory.JAVASCRIPT)]
        [TestCase(ReportCategory.LIVEKIT)]
        [TestCase(ReportCategory.PLACES)]
        public void DropWebSocketFailuresRegardlessOfCategory(string category)
        {
            var @event = new SentryEvent(new WebSocketException("The remote party closed the WebSocket connection without completing the close handshake."));
            @event.SetTag("category", category);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropWebSocketFailuresWrappedByTheRpcTransport()
        {
            var @event = new SentryEvent(new InvalidOperationException("RPC Transport failed", new WebSocketException()));
            @event.SetTag("category", ReportCategory.FRIENDS);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [TestCaseSource(nameof(NetworkingFailures))]
        public void DropNetworkingFailuresInLocalSceneDevelopment(Exception exception)
        {
            var @event = new SentryEvent(exception);
            @event.SetTag("category", ReportCategory.MULTIPLAYER);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event, isLocalSceneDevelopment: true));
        }

        [TestCaseSource(nameof(NetworkingFailures))]
        public void KeepNetworkingFailuresOutsideLocalSceneDevelopment(Exception exception)
        {
            var @event = new SentryEvent(exception);
            @event.SetTag("category", ReportCategory.MULTIPLAYER);

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event, isLocalSceneDevelopment: false));
        }

        [TestCase(502)]
        [TestCase(525)]
        public async Task DropTransientGatewayFailuresRegardlessOfCategory(int statusCode)
        {
            var @event = new SentryEvent(await NewFailedRequestExceptionAsync(statusCode));
            @event.SetTag("category", ReportCategory.PLACES);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public async Task DropTransientGatewayFailureWrappedByAnApiException()
        {
            var @event = new SentryEvent(new InvalidOperationException("Error fetching search places info:", await NewFailedRequestExceptionAsync(502)));
            @event.SetTag("category", ReportCategory.UI);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [TestCase(401)]
        [TestCase(404)]
        public async Task KeepDefinitiveHttpFailures(int statusCode)
        {
            var @event = new SentryEvent(await NewFailedRequestExceptionAsync(statusCode));
            @event.SetTag("category", ReportCategory.PLACES);

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropNetworkingFailureWrappedByUnobservedTaskInLocalSceneDevelopment()
        {
            var @event = new SentryEvent(new AggregateException(new SocketException((int)SocketError.ConnectionRefused)));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event, isLocalSceneDevelopment: true));
        }

        [Test]
        public void KeepOtherExceptionsInLocalSceneDevelopment()
        {
            var @event = new SentryEvent(new NullReferenceException());

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event, isLocalSceneDevelopment: true));
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
