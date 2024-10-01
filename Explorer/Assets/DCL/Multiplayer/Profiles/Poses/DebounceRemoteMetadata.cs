using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class DebounceRemoteMetadata : IRemoteMetadata
    {
        private readonly IRemoteMetadata origin;
        private Vector2Int? previous;

        public DebounceRemoteMetadata(IRemoteMetadata origin)
        {
            this.origin = origin;
        }

        public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata => origin.Metadata;

        public void BroadcastSelfPose(Vector2Int pose)
        {
            if (previous == pose)
                return;

            origin.BroadcastSelfPose(pose);
            previous = pose;
        }
    }
}
