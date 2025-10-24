using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using Microsoft.ClearScript.V8.FastProxy;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public sealed class CommsApiWrap : JsApiWrapper, IV8FastHostObject
    {
        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private static readonly V8FastHostObjectOperations<CommsApiWrap> OPERATIONS = new();
        IV8FastHostObjectOperations IV8FastHostObject.Operations => OPERATIONS;
        private const string EMPTY_RESPONSE = "{\"streams\":[]}";

        private readonly StringBuilder stringBuilder;
        private readonly StringWriter stringWriter;
        private readonly JsonWriter writer;

        static CommsApiWrap()
        {
            OPERATIONS.Configure(static configuration =>
            {
                configuration.AddMethodGetter(nameof(GetActiveVideoStreams),
                    static (CommsApiWrap self, in V8FastArgs _, in V8FastResult result) =>
                        result.Set(self.GetActiveVideoStreams()));
            });
        }

        public CommsApiWrap(IRoomHub roomHub, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.roomHub = roomHub;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            stringBuilder = new StringBuilder();
            stringWriter = new StringWriter(stringBuilder);
            writer = new JsonTextWriter(stringWriter);
        }

        public override void Dispose()
        {
            stringBuilder.Clear();
            stringWriter.Dispose();
            writer.Close();
        }

        private string GetActiveVideoStreams()
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
                                        remoteParticipantIdentity, track);

                                    if (asCurrent == null)
                                    {
                                        asCurrent = (remoteParticipantIdentity, track);

                                        GetActiveVideoStreamsResponse.WriteAsCurrentTo(writer,
                                            remoteParticipantIdentity, track);
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
    }
}
