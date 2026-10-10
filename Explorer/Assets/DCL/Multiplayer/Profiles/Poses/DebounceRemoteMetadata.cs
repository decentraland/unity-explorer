using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class DebounceRemoteMetadata : IRemoteMetadata
    {
        private readonly IRemoteMetadata origin;
        private Vector2Int? previous;

        public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata => origin.Metadata;

        public DebounceRemoteMetadata(IRemoteMetadata origin)
        {
            this.origin = origin;
        }

        public void BroadcastSelfParcel(Vector2Int pose)
        {
            if (previous == pose)
                return;

            origin.BroadcastSelfParcel(pose);
            previous = pose;
        }

        public void BroadcastSelfMetadata()
        {
            origin.BroadcastSelfMetadata();
        }
    }
}
