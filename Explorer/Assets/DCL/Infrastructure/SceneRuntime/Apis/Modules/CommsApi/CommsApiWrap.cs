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
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public sealed class CommsApiWrap : JsApiWrapper
    {
        private const int MAX_MESSAGES_PER_SECOND = 10;
        private const int RATE_LIMIT_WINDOW_MS = 1000;
        private const int MAX_TOPIC_LENGTH = 512;
        private const int MSG_TYPE_BYTE_SIZE = 1;
        private const int TOPIC_LENGTH_PREFIX_BYTES = sizeof(ushort);
        private const int MSG_TYPE_AND_TOPIC_LENGTH_PREFIX_SIZE = MSG_TYPE_BYTE_SIZE + TOPIC_LENGTH_PREFIX_BYTES;
        private const string EMPTY_RESPONSE = "{\"streams\":[]}";
        private const string EMPTY_ARRAY = "[]";

        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly string sceneId;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onDataReceivedCached;

        private readonly CommsWriter commsWriter = new ();

        private readonly ConcurrentDictionary<string, ConcurrentQueue<BufferedDataMessage>> topicBuffers = new ();
        private readonly ConcurrentDictionary<string, (int count, int windowStartMs)> publishRateLimiters = new ();

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
        public void PublishData(string topic, string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data))
                    return;

                if (!TryConsumeRateLimit(topic))
                    return;

                byte[] topicBytes = Encoding.UTF8.GetBytes(topic);

                if (topicBytes.Length > MAX_TOPIC_LENGTH)
                    return;

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);

                // Wire format: [MsgType.CommsData 1 byte][topicLen 2 bytes LE][topic UTF-8][data UTF-8].
                int payloadLength = TOPIC_LENGTH_PREFIX_BYTES + topicBytes.Length + dataBytes.Length;

                if (MSG_TYPE_BYTE_SIZE + payloadLength > IJsOperations.LIVEKIT_MAX_SIZE)
                    return;

                Span<byte> encoded = stackalloc byte[MSG_TYPE_BYTE_SIZE + payloadLength];
                encoded[0] = (byte)ISceneCommunicationPipe.MsgType.CommsData;
                BinaryPrimitives.WriteUInt16LittleEndian(encoded[MSG_TYPE_BYTE_SIZE..], (ushort)topicBytes.Length);
                topicBytes.AsSpan().CopyTo(encoded[MSG_TYPE_AND_TOPIC_LENGTH_PREFIX_SIZE..]);
                dataBytes.AsSpan().CopyTo(encoded[(MSG_TYPE_AND_TOPIC_LENGTH_PREFIX_SIZE + topicBytes.Length)..]);

                sceneCommunicationPipe.SendMessage(
                    encoded, sceneId,
                    ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED,
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
            topicBuffers.TryAdd(topic, new ConcurrentQueue<BufferedDataMessage>());
        }

        /// <summary>
        /// Registers unsubscribtion intent. Messages on this topic will be not received after the operation.
        /// Called from JS via ClearScript.
        /// </summary>
        [UsedImplicitly]
        public void UnsubscribeFromTopic(string topic)
        {
            topicBuffers.TryRemove(topic, out ConcurrentQueue<BufferedDataMessage> _output);
            // 'output' object is droped and will be collected by GC (it's assumed nothing else holds the reference)
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
                if (!topicBuffers.TryGetValue(topic, out ConcurrentQueue<BufferedDataMessage> queue) || queue.IsEmpty)
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
        /// Runs on the LiveKit callback thread (ORIGIN_THREAD), not the main thread.
        /// Only thread-safe types (ConcurrentQueue, Encoding) are used here.
        /// Decodes wire format: [topicLen 2 bytes LE][topic UTF-8][data UTF-8].
        /// </summary>
        private void OnDataReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            ReadOnlySpan<byte> span = message.Data;

            if (span.Length < TOPIC_LENGTH_PREFIX_BYTES) return;

            ushort topicLength = BinaryPrimitives.ReadUInt16LittleEndian(span);

            if (span.Length < TOPIC_LENGTH_PREFIX_BYTES + topicLength) return;

            string topic = Encoding.UTF8.GetString(span.Slice(TOPIC_LENGTH_PREFIX_BYTES, topicLength));

            if (!topicBuffers.TryGetValue(topic, out ConcurrentQueue<BufferedDataMessage> queue))
                return;

            string data = Encoding.UTF8.GetString(span[(TOPIC_LENGTH_PREFIX_BYTES + topicLength)..]);
            queue.Enqueue(new BufferedDataMessage(message.FromWalletId, data));
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
