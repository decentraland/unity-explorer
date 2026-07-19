using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     The server-to-client half of the MCP Streamable HTTP transport: the SSE streams a client opens with
    ///     GET to receive server-initiated JSON-RPC notifications. Subscribers are grouped by a named stream
    ///     (e.g. "scene", "client") and a minimum level, so a client subscribes to exactly one source at one
    ///     verbosity. Thread-safe: log callbacks publish from arbitrary threads.
    /// </summary>
    public sealed class McpNotificationChannel : IDisposable
    {
        /// <summary>The write target behind a subscriber — an SSE-carrying HTTP response, or a fake in tests.</summary>
        public interface ISseSink
        {
            /// <summary>Writes one already-framed SSE chunk; returns false once the client is gone.</summary>
            bool TryWrite(byte[] bytes);

            void Close();
        }

        private readonly object gate = new ();
        private readonly List<Subscriber> subscribers = new ();
        private bool disposed;

        /// <summary>Cheap pre-check so a log source can skip formatting when no one is listening on a stream.</summary>
        public bool HasSubscribers(string stream)
        {
            lock (gate)
            {
                foreach (Subscriber s in subscribers)
                    if (s.Stream == stream)
                        return true;

                return false;
            }
        }

        /// <summary>
        ///     Registers a sink under a stream + minimum level. Returns a handle that removes it (safe to call
        ///     more than once), or null if the channel is already disposed.
        /// </summary>
        public IDisposable? Add(string stream, McpLogLevel minLevel, ISseSink sink)
        {
            var subscriber = new Subscriber(stream, minLevel, sink);

            lock (gate)
            {
                if (disposed) return null;
                subscribers.Add(subscriber);
            }

            return new Subscription(this, subscriber);
        }

        /// <summary>
        ///     Frames a JSON-RPC notification as one SSE event and writes it to every subscriber of
        ///     <paramref name="stream" /> whose minimum level admits <paramref name="level" />; drops any that fail.
        /// </summary>
        public void Publish(string stream, McpLogLevel level, JObject notification)
        {
            byte[]? frame = null;
            List<Subscriber>? dead = null;

            lock (gate)
            {
                foreach (Subscriber s in subscribers)
                {
                    if (s.Stream != stream || level < s.MinLevel) continue;

                    frame ??= Frame(notification);

                    if (s.Sink.TryWrite(frame)) continue;

                    dead ??= new List<Subscriber>();
                    dead.Add(s);
                }

                if (dead != null)
                    foreach (Subscriber s in dead)
                    {
                        subscribers.Remove(s);
                        s.Sink.Close();
                    }
            }
        }

        // "data: <single-line json>\n\n" — the JSON is serialized without newlines, so one event is one line.
        private static byte[] Frame(JObject notification) =>
            Encoding.UTF8.GetBytes($"data: {notification.ToString(Formatting.None)}\n\n");

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                foreach (Subscriber s in subscribers) s.Sink.Close();
                subscribers.Clear();
            }
        }

        private void Remove(Subscriber subscriber)
        {
            lock (gate)
            {
                if (subscribers.Remove(subscriber))
                    subscriber.Sink.Close();
            }
        }

        private sealed class Subscriber
        {
            public readonly string Stream;
            public readonly McpLogLevel MinLevel;
            public readonly ISseSink Sink;

            public Subscriber(string stream, McpLogLevel minLevel, ISseSink sink)
            {
                Stream = stream;
                MinLevel = minLevel;
                Sink = sink;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly McpNotificationChannel channel;
            private readonly Subscriber subscriber;
            private bool removed;

            public Subscription(McpNotificationChannel channel, Subscriber subscriber)
            {
                this.channel = channel;
                this.subscriber = subscriber;
            }

            public void Dispose()
            {
                if (removed) return;
                removed = true;
                channel.Remove(subscriber);
            }
        }
    }
}
