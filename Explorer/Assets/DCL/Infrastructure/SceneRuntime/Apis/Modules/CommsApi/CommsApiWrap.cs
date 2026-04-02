using DCL.Multiplayer.Connections.RoomHubs;
using JetBrains.Annotations;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public sealed class CommsApiWrap : JsApiWrapper
    {
        private const int MAX_MESSAGES_PER_SECOND = 10;
        private const int RATE_LIMIT_WINDOW_MS = 1000;
        private const string EMPTY_RESPONSE = "{\"streams\":[]}";
        private const string EMPTY_ARRAY = "[]";

        private static readonly string[] EMPTY_DESTINATIONS = Array.Empty<string>();

        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly IDataPipe dataPipe;

        private readonly StringBuilder stringBuilder;
        private readonly StringWriter stringWriter;
        private readonly JsonWriter writer;

        private readonly ConcurrentDictionary<string, ConcurrentQueue<BufferedDataMessage>> topicBuffers = new ();
        private readonly ConcurrentDictionary<string, (int count, int windowStartMs)> publishRateLimiters = new ();
        private byte[] publishBuffer = Array.Empty<byte>();

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

        public CommsApiWrap(IRoomHub roomHub, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.roomHub = roomHub;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            stringBuilder = new StringBuilder();
            stringWriter = new StringWriter(stringBuilder);
            writer = new JsonTextWriter(stringWriter);

            dataPipe = roomHub.StreamingRoom().DataPipe;
            dataPipe.DataReceived += OnDataReceived;
        }

        public override void Dispose()
        {
            dataPipe.DataReceived -= OnDataReceived;
            topicBuffers.Clear();
            publishRateLimiters.Clear();
            stringBuilder.Clear();
            stringWriter.Dispose();
            writer.Close();
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
                    (string identity, TrackPublication publication)? asCurrent = null;

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

                                    if (asCurrent == null)
                                    {
                                        asCurrent = (remoteParticipantIdentity, track);

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

        [UsedImplicitly]
        public void PublishData(string topic, ITypedArray<byte> data)
        {
            try
            {
                if (data == null || data.Length == 0 || data.Length > (ulong)IJsOperations.LIVEKIT_MAX_SIZE)
                    return;

                if (!TryConsumeRateLimit(topic))
                    return;

                int length = (int)data.Length;

                if (publishBuffer.Length < length)
                    publishBuffer = new byte[length];

                data.Read(0, (ulong)length, publishBuffer, 0);
                roomHub.StreamingRoom().DataPipe.PublishData(publishBuffer.AsSpan(0, length), topic, EMPTY_DESTINATIONS, DataPacketKind.KindReliable);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        [UsedImplicitly]
        public void UpdateMetadata(string metadata)
        {
            try
            {
                roomHub.StreamingRoom().UpdateLocalMetadata(metadata);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        [UsedImplicitly]
        public void SubscribeToTopic(string topic)
        {
            topicBuffers.TryAdd(topic, new ConcurrentQueue<BufferedDataMessage>());
        }

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

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, string topic, DataPacketKind kind)
        {
            if (!topicBuffers.TryGetValue(topic, out ConcurrentQueue<BufferedDataMessage> queue))
                return;

            if (participant == null)
                return;

            string decoded = Encoding.UTF8.GetString(data);
            queue.Enqueue(new BufferedDataMessage(participant.Identity, decoded));
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
    }
}
