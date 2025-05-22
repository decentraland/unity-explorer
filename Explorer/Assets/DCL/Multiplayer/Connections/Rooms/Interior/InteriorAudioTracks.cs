using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorAudioTracks : IAudioTracks, IInterior<IAudioTracks>
    {
        private IAudioTracks assigned = NullAudioTracks.INSTANCE;

        public ITrack CreateAudioTrack(string name, RtcAudioSource source, IRoom room) =>
            assigned.EnsureAssigned().CreateAudioTrack(name, source, room);

        public void Assign(IAudioTracks value, out IAudioTracks? previous)
        {
            previous = assigned;
            assigned = value;
            previous = previous is NullAudioTracks ? null : previous;
        }
    }
}
