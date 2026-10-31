using Cysharp.Threading.Tasks;
using DCL.Diagnostics.Sentry;
using NUnit.Framework;
using Sentry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Networking;

namespace DCL.Diagnostics.Tests
{
    public class SentryReportHandlerShould
    {
        private const string TASK_ALREADY_COMPLETED = "An attempt was made to transition a task to a final state when it had already completed.";
        private const int TOO_MANY_REQUESTS = 429;
        private const string EVENTS_RATE_LIMIT_BODY = "{\"ok\":false,\"error\":\"Too many requests\",\"code\":\"too_many_requests\"}";
        private const string TRANSLATION_RATE_LIMIT_BODY = "{\"error\":\"Slowdown: 360 per 1 minute\"}";
        private static readonly TimeSpan RESPONSE_TIMEOUT = TimeSpan.FromSeconds(10);

        private static readonly SceneShortInfo SCENE_INFO = new (Vector2Int.zero, "kingdom-of-antrom");

        private static Scope NewScope() =>
            new (new SentryOptions());

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
        public async Task DropRateLimitAnswersOfTheEventsServer()
        {
            var @event = new SentryEvent(await ReceiveErrorAnswerAsync(TOO_MANY_REQUESTS, EVENTS_RATE_LIMIT_BODY));

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public async Task KeepRateLimitAnswersOfOtherServices()
        {
            var @event = new SentryEvent(await ReceiveErrorAnswerAsync(TOO_MANY_REQUESTS, TRANSLATION_RATE_LIMIT_BODY));

            Assert.IsNotNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void DropUnreachableThirdwebBundler()
        {
            var @event = new SentryEvent(new Exception("Bundler Request Failed. Error: 0 - "));
            @event.SetTag("category", ReportCategory.AUTHENTICATION);

            Assert.IsNull(SentryReportHandler.BeforeSend(@event));
        }

        [Test]
        public void KeepThirdwebBundlerFailuresWithAStatus()
        {
            var @event = new SentryEvent(new Exception("Bundler Request Failed. Error: 500 - {\"error\":\"internal\"}"));
            @event.SetTag("category", ReportCategory.AUTHENTICATION);

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

        /// <summary>
        ///     A <see cref="UnityWebRequestException" /> takes its status and body from a sent request only,
        ///     so a loopback listener answers one real request with them.
        /// </summary>
        private static async Task<UnityWebRequestException> ReceiveErrorAnswerAsync(int statusCode, string body)
        {
            int port = FreeLoopbackPort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            try
            {
                Task answer = AnswerOnceAsync(listener, statusCode, body);

                using UnityWebRequest request = UnityWebRequest.Get($"http://127.0.0.1:{port}/api/events");
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                DateTime deadline = DateTime.UtcNow + RESPONSE_TIMEOUT;

                // Task.Delay rather than UniTask: EditMode tests do not run the Player Loop in playing state
                while (!operation.isDone && DateTime.UtcNow < deadline)
                    await Task.Delay(10);

                await answer;

                Assert.AreEqual(statusCode, request.responseCode);
                return new UnityWebRequestException(request);
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        private static async Task AnswerOnceAsync(HttpListener listener, int statusCode, string body)
        {
            HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
            byte[] content = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = content.Length;
            await context.Response.OutputStream.WriteAsync(content, 0, content.Length).ConfigureAwait(false);
            context.Response.Close();
        }

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return freePort;
        }
    }
}
