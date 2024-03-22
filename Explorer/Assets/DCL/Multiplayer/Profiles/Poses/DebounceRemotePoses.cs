using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class DebounceRemotePoses : IRemotePoses
    {
        private readonly IRemotePoses origin;
        private Vector2Int? previous;

        public int Count => origin.Count;

        public DebounceRemotePoses(IRemotePoses origin)
        {
            this.origin = origin;
        }

        public Vector2Int ParcelPose(string walletId) =>
            origin.ParcelPose(walletId);

        public void BroadcastSelfPose(Vector2Int pose)
        {
            if (previous == pose)
                return;

            origin.BroadcastSelfPose(pose);
            previous = pose;
        }

        public IEnumerator<KeyValuePair<string, Vector2Int>> GetEnumerator() =>
            origin.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
