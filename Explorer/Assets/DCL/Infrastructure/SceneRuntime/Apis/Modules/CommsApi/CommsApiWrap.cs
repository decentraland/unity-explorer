using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using Microsoft.ClearScript.V8.SplitProxy;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.IO;
using System.Text;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public class CommsApiWrap : IJsApiWrapper, IV8HostObject
    {
        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly InvokeHostObject getActiveVideoStreams;
        private const string EMPTY_RESPONSE = "{\"streams\":[]}";

        private readonly StringBuilder stringBuilder;
        private readonly StringWriter stringWriter;
        private readonly JsonWriter writer;

        public CommsApiWrap(IRoomHub roomHub, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            this.roomHub = roomHub;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            getActiveVideoStreams = GetActiveVideoStreams;

            stringBuilder = new StringBuilder();
            stringWriter = new StringWriter(stringBuilder);
            writer = new JsonTextWriter(stringWriter);
        }

        public void Dispose()
        {
            stringBuilder.Clear();
            stringWriter.Dispose();
            writer.Close();
        }

        private void GetActiveVideoStreams(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            string response = EMPTY_RESPONSE;

            try { response = GetActiveVideoStreams(); }
            catch (Exception e) { sceneExceptionsHandler.OnEngineException(e); }

            result.SetString(response);
        }

        private string GetActiveVideoStreams()
        {
            lock (this)
            {
                stringBuilder.Clear();

                writer.WriteStartObject();
                writer.WritePropertyName("streams");
                writer.WriteStartArray();

                var participants = roomHub.StreamingRoom().Participants;
                (string identity, TrackPublication publication)? asCurrent = null;

                foreach (string remoteParticipantIdentity in participants.RemoteParticipantIdentities())
                {
                    var participant = participants.RemoteParticipant(remoteParticipantIdentity);

                    if (participant == null)
                        continue;

                    foreach (var track in participant.Tracks.Values)
                        if (track.Kind == TrackKind.KindVideo)
                        {
                            GetActiveVideoStreamsResponse.WriteTo(writer, remoteParticipantIdentity, track);

                            if (asCurrent == null)
                            {
                                asCurrent = (remoteParticipantIdentity, track);
                                GetActiveVideoStreamsResponse.WriteAsCurrentTo(writer, remoteParticipantIdentity, track);
                            }
                        }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                return stringWriter.ToString();
            }
        }

        void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(GetActiveVideoStreams)))
                value.SetHostObject(getActiveVideoStreams);
            else
                throw new NotImplementedException(
                    $"Named property {name.ToString()} is not implemented");
        }
    }
}
