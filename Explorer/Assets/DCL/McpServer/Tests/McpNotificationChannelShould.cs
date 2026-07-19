using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace DCL.McpServer.Tests
{
    public class McpNotificationChannelShould
    {
        private static JObject Note(string body) =>
            new () { ["jsonrpc"] = "2.0", ["method"] = "notifications/message", ["params"] = new JObject { ["data"] = body } };

        private sealed class FakeSink : McpNotificationChannel.ISseSink
        {
            public readonly List<string> Frames = new ();
            public bool Closed;
            public bool FailWrites;

            public bool TryWrite(byte[] bytes)
            {
                if (FailWrites) return false;
                Frames.Add(Encoding.UTF8.GetString(bytes));
                return true;
            }

            public void Close() => Closed = true;
        }

        [Test]
        public void DeliverToSubscribersOfTheMatchingStreamOnly()
        {
            using var channel = new McpNotificationChannel();
            var scene = new FakeSink();
            var client = new FakeSink();
            channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, scene);
            channel.Add(McpLogNotifierStreams.CLIENT, McpLogLevel.Debug, client);

            channel.Publish(McpLogNotifierStreams.SCENE, McpLogLevel.Info, Note("hi"));

            Assert.That(scene.Frames, Has.Count.EqualTo(1));
            Assert.That(client.Frames, Is.Empty);
        }

        [Test]
        public void FrameNotificationsAsSingleLineSseEvents()
        {
            using var channel = new McpNotificationChannel();
            var sink = new FakeSink();
            channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, sink);

            channel.Publish(McpLogNotifierStreams.SCENE, McpLogLevel.Info, Note("hello"));

            Assert.That(sink.Frames[0], Does.StartWith("data: "));
            Assert.That(sink.Frames[0], Does.EndWith("\n\n"));
            Assert.That(sink.Frames[0], Does.Contain("\"hello\""));
        }

        [Test]
        public void DropEntriesBelowTheSubscriptionLevel()
        {
            using var channel = new McpNotificationChannel();
            var sink = new FakeSink();
            channel.Add(McpLogNotifierStreams.CLIENT, McpLogLevel.Warning, sink);

            channel.Publish(McpLogNotifierStreams.CLIENT, McpLogLevel.Info, Note("noise"));
            channel.Publish(McpLogNotifierStreams.CLIENT, McpLogLevel.Error, Note("real"));

            Assert.That(sink.Frames, Has.Count.EqualTo(1));
            Assert.That(sink.Frames[0], Does.Contain("real"));
        }

        [Test]
        public void DropAndCloseASinkThatFailsToWrite()
        {
            using var channel = new McpNotificationChannel();
            var sink = new FakeSink { FailWrites = true };
            channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, sink);

            channel.Publish(McpLogNotifierStreams.SCENE, McpLogLevel.Error, Note("x"));
            Assert.That(sink.Closed, Is.True);

            // The dead sink was removed; a second publish must not touch it again.
            sink.Closed = false;
            channel.Publish(McpLogNotifierStreams.SCENE, McpLogLevel.Error, Note("y"));
            Assert.That(sink.Closed, Is.False);
        }

        [Test]
        public void StopDeliveringAfterUnsubscribe()
        {
            using var channel = new McpNotificationChannel();
            var sink = new FakeSink();
            var handle = channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, sink);

            handle!.Dispose();
            channel.Publish(McpLogNotifierStreams.SCENE, McpLogLevel.Error, Note("after"));

            Assert.That(sink.Frames, Is.Empty);
            Assert.That(sink.Closed, Is.True);
        }

        [Test]
        public void CloseAllSinksAndRefuseNewOnesOnDispose()
        {
            var channel = new McpNotificationChannel();
            var sink = new FakeSink();
            channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, sink);

            channel.Dispose();

            Assert.That(sink.Closed, Is.True);
            Assert.That(channel.Add(McpLogNotifierStreams.SCENE, McpLogLevel.Debug, new FakeSink()), Is.Null);
        }

        // Mirror of McpLogNotifier's stream names, kept local so the test asserts the channel in isolation.
        private static class McpLogNotifierStreams
        {
            public const string SCENE = "scene";
            public const string CLIENT = "client";
        }
    }
}
