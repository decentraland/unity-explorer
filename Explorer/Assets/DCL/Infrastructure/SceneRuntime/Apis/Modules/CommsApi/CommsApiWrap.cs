using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using JetBrains.Annotations;
using LiveKit.Proto;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Utility.Multithreading;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public sealed class CommsApiWrap : JsApiWrapper
    {
        private const int MAX_MESSAGES_PER_SECOND = 10;
        private const int RATE_LIMIT_WINDOW_MS = 1000;

        private const int MAX_TOPIC_BYTES_LENGTH = 512;
        private const int MSG_TYPE_BYTE_SIZE = 1;
        private const int TOPIC_LENGTH_PREFIX_BYTES = sizeof(ushort);

        private const int TOPIC_BUFFER_MAX_MESSAGE_COUNT = 1024;

        private const string EMPTY_RESPONSE = "{\"streams\":[]}";
        private const string EMPTY_ARRAY = "[]";

        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly string sceneId;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onDataReceivedCached;

        private readonly CommsWriter commsWriter = new ();

        private readonly DCLConcurrentDictionary<string, DCLConcurrentQueue<BufferedDataMessage>> topicBuffers = new ();
        private readonly DCLConcurrentDictionary<string, (int count, int windowStartMs)> publishRateLimiters = new ();

        private readonly object topicLookupLock = new ();

        // Copy-on-write byte-keyed snapshot of topicBuffers (queues shared, not copied) so OnDataReceived
        // can match the wire topic without allocating a string per message (Unity's BCL has no span-keyed
        // dictionary lookup). Writers rebuild under topicLookupLock; the volatile publish guarantees the
        // LiveKit-thread reader a complete snapshot, never a partial one.
        private volatile TopicLookupEntry[] topicLookup = Array.Empty<TopicLookupEntry>();

        public CommsApiWrap(
            IRoomHub roomHub,
            ISceneCommunicationPipe sceneCommunicationPipe,
            ISceneData sceneData,
            ISceneExceptionsHandler sceneExceptionsHandler,
            CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.roomHub = roomHub;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
            sceneId = sceneData.SceneEntityDefinition.id!;
            this.sceneExceptionsHandler = sceneExceptionsHandler;

            onDataReceivedCached = OnDataReceived;
            sceneCommunicationPipe.AddSceneMessageHandler(sceneId, ISceneCommunicationPipe.MsgType.CommsData, onDataReceivedCached);
        }

        public override void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, ISceneCommunicationPipe.MsgType.CommsData, onDataReceivedCached);
            topicBuffers.Clear();

            lock (topicLookupLock)
            {
                topicLookup = Array.Empty<TopicLookupEntry>();
            }

            publishRateLimiters.Clear();
            commsWriter.Dispose();
        }

        public string GetActiveVideoStreams()
        {
            try
            {
                lock (this)
                {
                    using CommsWriter.Scope scope = commsWriter.Begin();
                    JsonTextWriter writer = scope.Writer;

                    writer.WriteStartObject();
                    writer.WritePropertyName("streams");
                    writer.WriteStartArray();

                    var participants = roomHub.StreamingRoom().Participants;
                    bool currentWritten = false;

                    // See: https://github.com/decentraland/unity-explorer/issues/3796
                    lock (participants)
                    {
                        foreach ((string remoteParticipantIdentity, _) in participants.RemoteParticipantIdentities())
                        {
                            var participant = participants.RemoteParticipant(remoteParticipantIdentity);

                            if (participant == null)
                                continue;

                            foreach (var track in participant.Tracks.Values)
                            {
                                if (track.Kind == TrackKind.KindVideo)
                                {
                                    GetActiveVideoStreamsResponse.WriteTo(writer,
                                        remoteParticipantIdentity, participant, track);

                                    if (!currentWritten)
                                    {
                                        currentWritten = true;

                                        GetActiveVideoStreamsResponse.WriteAsCurrentTo(writer,
                                            remoteParticipantIdentity, participant, track);
                                    }
                                }
                            }
                        }
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();

                    return scope.Complete();
                }
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
                return EMPTY_RESPONSE;
            }
        }

        /// <summary>
        /// Publishes a JSON string to a topic on the scene's LiveKit room.
        /// Wire format after MsgType byte: [topicLen 2 bytes LE][topic UTF-8][data UTF-8].
        /// Called from JS via ClearScript. Rate-limited to <see cref="MAX_MESSAGES_PER_SECOND"/> per topic.
        /// </summary>
        [UsedImplicitly]
        public void PublishData(string topic, string? data)
        {
            try
            {
                if (string.IsNullOrEmpty(data))
                    return;

                if (!TryConsumeRateLimit(topic))
                    return;

                int topicBytesCount = Encoding.UTF8.GetByteCount(topic);

                if (topicBytesCount > MAX_TOPIC_BYTES_LENGTH)
                    return;

                int dataBytesCount = Encoding.UTF8.GetByteCount(data);

                // Wire format: [MsgType.CommsData 1 byte][topicLen 2 bytes LE][topic UTF-8][data UTF-8].
                int payloadLength = TOPIC_LENGTH_PREFIX_BYTES + topicBytesCount + dataBytesCount;

                int totalLength = MSG_TYPE_BYTE_SIZE + payloadLength;

                if (totalLength > IJsOperations.LIVEKIT_MAX_SIZE)
                    return;

                Span<byte> encoded = stackalloc byte[totalLength];

                // write MsgType
                encoded[0] = (byte)ISceneCommunicationPipe.MsgType.CommsData;

                // write topic length
                Span<byte> encodedWriteTarget = encoded.Slice(MSG_TYPE_BYTE_SIZE);
                BinaryPrimitives.WriteUInt16LittleEndian(encodedWriteTarget, (ushort)topicBytesCount);

                // write topic
                encodedWriteTarget = encodedWriteTarget.Slice(TOPIC_LENGTH_PREFIX_BYTES);
                int writtenTopicBytes = Encoding.UTF8.GetBytes(topic, encodedWriteTarget);
                UnityEngine.Assertions.Assert.AreEqual(topicBytesCount, writtenTopicBytes);

                // write data
                encodedWriteTarget = encodedWriteTarget.Slice(topicBytesCount);
                int writtenDataBytes = Encoding.UTF8.GetBytes(data, encodedWriteTarget);
                UnityEngine.Assertions.Assert.AreEqual(dataBytesCount, writtenDataBytes);

                sceneCommunicationPipe.SendMessage(
                    encoded, sceneId,
                    ISceneCommunicationPipe.ConnectivityAssertiveness.DropIfNotConnected,
                    disposeCts.Token);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        /// <summary>
        /// Registers interest in a topic. Messages on this topic will be buffered for later consumption.
        /// Called from JS via ClearScript.
        /// </summary>
        [UsedImplicitly]
        public void SubscribeToTopic(string topic)
        {
            // method is called relatively rare, allocation new Queue is acceptable, pooling not required
            if (topicBuffers.TryAdd(topic, new DCLConcurrentQueue<BufferedDataMessage>()))
                RebuildTopicLookup();
        }

        /// <summary>
        /// Registers unsubscribtion intent. Messages on this topic will be not received after the operation.
        /// Called from JS via ClearScript.
        /// </summary>
        [UsedImplicitly]
        public void UnsubscribeFromTopic(string topic)
        {
            if (topicBuffers.TryRemove(topic, out _))
                RebuildTopicLookup();

            // the removed queue is dropped and will be collected by GC (it's assumed nothing else holds the reference)
        }

        /// <summary>
        /// Returns and drains all buffered messages for a topic as a JSON array.
        /// Called from JS via ClearScript.
        /// </summary>
        [UsedImplicitly]
        public string ConsumeMessages(string topic)
        {
            try
            {
                if (!topicBuffers.TryGetValue(topic, out DCLConcurrentQueue<BufferedDataMessage> queue) || queue.IsEmpty)
                    return EMPTY_ARRAY;

                lock (this)
                {
                    using CommsWriter.Scope scope = commsWriter.Begin();
                    JsonTextWriter writer = scope.Writer;

                    writer.WriteStartArray();

                    while (queue.TryDequeue(out BufferedDataMessage msg))
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("sender");
                        writer.WriteValue(msg.SenderIdentity);
                        writer.WritePropertyName("data");
                        writer.WriteValue(msg.Data);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();

                    return scope.Complete();
                }
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
                return EMPTY_ARRAY;
            }
        }

        /// <summary>
        /// Runs on the LiveKit callback thread (ORIGIN_THREAD) for all scene CommsData traffic;
        /// must not allocate for messages on unsubscribed topics.
        /// Decodes wire format: [topicLen 2 bytes LE][topic UTF-8][data UTF-8].
        /// </summary>
        private void OnDataReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            ReadOnlySpan<byte> span = message.Data;

            if (span.Length < TOPIC_LENGTH_PREFIX_BYTES) return;

            ushort topicLength = BinaryPrimitives.ReadUInt16LittleEndian(span);

            if (span.Length < TOPIC_LENGTH_PREFIX_BYTES + topicLength) return;

            ReadOnlySpan<byte> topicSpan = span.Slice(TOPIC_LENGTH_PREFIX_BYTES, topicLength);

            // Scenes subscribe to a handful of topics; a linear scan avoids the string key a hash lookup would need.
            TopicLookupEntry[] lookup = topicLookup;

            for (var i = 0; i < lookup.Length; i++)
            {
                if (!topicSpan.SequenceEqual(lookup[i].Utf8Topic))
                    continue;

                DCLConcurrentQueue<BufferedDataMessage> queue = lookup[i].Queue;

                // DROP OLD POLICY. Dequeues oldest item to insert new one
                if (queue.Count >= TOPIC_BUFFER_MAX_MESSAGE_COUNT)
                {
                    queue.TryDequeue(out _);
                }

                string data = Encoding.UTF8.GetString(span[(TOPIC_LENGTH_PREFIX_BYTES + topicLength)..]);
                queue.Enqueue(new BufferedDataMessage(message.FromWalletId, data));
                return;
            }
        }

        private void RebuildTopicLookup()
        {
            // Allocates freely (list, byte[] per topic, final array): it only runs on subscribe/unsubscribe,
            // which happens a handful of times per scene lifetime. COW trades allocation on this rare write
            // path for allocation-free reads in OnDataReceived; published arrays are never mutated or reused.
            lock (topicLookupLock)
            {
                var entries = new List<TopicLookupEntry>(topicBuffers.Count);

                foreach (KeyValuePair<string, DCLConcurrentQueue<BufferedDataMessage>> pair in topicBuffers)
                    entries.Add(new TopicLookupEntry(Encoding.UTF8.GetBytes(pair.Key), pair.Value));

                topicLookup = entries.ToArray();
            }
        }

        private bool TryConsumeRateLimit(string topic)
        {
            int nowMs = Environment.TickCount;

            if (!publishRateLimiters.TryGetValue(topic, out (int count, int windowStartMs) limiter))
            {
                publishRateLimiters[topic] = (1, nowMs);
                return true;
            }

            int elapsed = nowMs - limiter.windowStartMs;

            if (elapsed >= RATE_LIMIT_WINDOW_MS || elapsed < 0)
            {
                publishRateLimiters[topic] = (1, nowMs);
                return true;
            }

            if (limiter.count >= MAX_MESSAGES_PER_SECOND)
                return false;

            publishRateLimiters[topic] = (limiter.count + 1, limiter.windowStartMs);
            return true;
        }

        private readonly struct TopicLookupEntry
        {
            public readonly byte[] Utf8Topic;
            public readonly DCLConcurrentQueue<BufferedDataMessage> Queue;

            public TopicLookupEntry(byte[] utf8Topic, DCLConcurrentQueue<BufferedDataMessage> queue)
            {
                Utf8Topic = utf8Topic;
                Queue = queue;
            }
        }

        private readonly struct BufferedDataMessage
        {
            public readonly string SenderIdentity;
            public readonly string Data;

            public BufferedDataMessage(string senderIdentity, string data)
            {
                SenderIdentity = senderIdentity;
                Data = data;
            }
        }

        /// <summary>
        /// Encapsulates for integrity, and correctness of JsonTextWriter writer, avoids state corruption.
        /// Implements RAII pattern to ensure the guarantees.
        /// NOT thread-safe.
        /// </summary>
        private class CommsWriter : IDisposable
        {
            private readonly StringBuilder stringBuilder;
            private StringWriter stringWriter;
            private JsonTextWriter writer;


            public CommsWriter()
            {
                stringBuilder = new StringBuilder();
                stringWriter = new StringWriter(stringBuilder);
                writer = new JsonTextWriter(stringWriter);
            }

            public void Dispose()
            {
                stringBuilder.Clear();
                writer.Close();
                stringWriter.Dispose();
            }

            /// <summary>
            /// Recreates JsonTextWriter after exceptions to avoid corrupted internal state
            /// (unbalance depth/token stack causing invalid JSON or exceptions)
            /// </summary>
            private void ResetWriter()
            {
                try { writer.Close(); }
                catch
                { /* writer may already be corrupted */
                }

                stringWriter.Dispose();

                stringWriter = new StringWriter(stringBuilder);
                writer = new JsonTextWriter(stringWriter);
            }

            public Scope Begin()
            {
                stringBuilder.Clear(); // always clear buffer at begin
                return new Scope(this);
            }

            public ref struct Scope
            {
                private readonly CommsWriter commsWriter;
                private bool isComplete;

                public JsonTextWriter Writer => commsWriter.writer;

                public Scope(CommsWriter commsWriter) : this()
                {
                    this.commsWriter = commsWriter;
                    this.isComplete = false;
                }

                public string Complete()
                {
                    if (isComplete)
                    {
                        ReportHub.LogError(
                            ReportCategory.COMMS_API,
                            "Cannot complete twice, make sure complete is called once per scope"
                        );
                    }

                    isComplete = true;
                    return commsWriter.stringWriter.ToString();
                }

                public void Dispose()
                {
                    // drop JsonTextWriter if complition was not performed gracefully.
                    if (isComplete == false)
                        commsWriter.ResetWriter();
                }
            }
        }
    }
}
