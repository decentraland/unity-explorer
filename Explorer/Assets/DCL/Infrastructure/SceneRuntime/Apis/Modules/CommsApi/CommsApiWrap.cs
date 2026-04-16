using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.Multiplayer.Connections.RoomHubs;
using JetBrains.Annotations;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
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
        private const string EMPTY_RESPONSE = "{\"streams\":[]}";
        private const string EMPTY_ARRAY = "[]";

        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly string sceneId;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onDataReceivedCached;

        private readonly StringBuilder stringBuilder;
        private readonly StringWriter stringWriter;
        private readonly JsonWriter writer;

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
            stringBuilder = new StringBuilder();
            stringWriter = new StringWriter(stringBuilder);
            writer = new JsonTextWriter(stringWriter);

            onDataReceivedCached = OnDataReceived;
            sceneCommunicationPipe.AddSceneMessageHandler(sceneId, ISceneCommunicationPipe.MsgType.CommsData, onDataReceivedCached);
        }

        public override void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, ISceneCommunicationPipe.MsgType.CommsData, onDataReceivedCached);
            topicBuffers.Clear();
            publishRateLimiters.Clear();
            stringBuilder.Clear();
            writer.Close();
            stringWriter.Dispose();
        }

        public string GetActiveVideoStreams()
        {
            try
            {
                lock (this)
                {
                    stringBuilder.Clear();

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

                    return stringWriter.ToString();
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
                int payloadLength = 2 + topicBytes.Length + dataBytes.Length;

                if (1 + payloadLength > IJsOperations.LIVEKIT_MAX_SIZE)
                    return;

                Span<byte> encoded = stackalloc byte[1 + payloadLength];
                encoded[0] = (byte)ISceneCommunicationPipe.MsgType.CommsData;
                BinaryPrimitives.WriteUInt16LittleEndian(encoded[1..], (ushort)topicBytes.Length);
                topicBytes.AsSpan().CopyTo(encoded[3..]);
                dataBytes.AsSpan().CopyTo(encoded[(3 + topicBytes.Length)..]);

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
            topicBuffers.TryAdd(topic, new ConcurrentQueue<BufferedDataMessage>());
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
                    stringBuilder.Clear();

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

                    return stringWriter.ToString();
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

            if (span.Length < 2) return;

            ushort topicLength = BinaryPrimitives.ReadUInt16LittleEndian(span);

            if (span.Length < 2 + topicLength) return;

            string topic = Encoding.UTF8.GetString(span.Slice(2, topicLength));

            if (!topicBuffers.TryGetValue(topic, out ConcurrentQueue<BufferedDataMessage> queue))
                return;

            string data = Encoding.UTF8.GetString(span[(2 + topicLength)..]);
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
    }
}
