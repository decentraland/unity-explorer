using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class DebounceRemotePoses : IRemotePoses
    {
        private readonly IRemotePoses origin;
        private Vector2Int? previous;

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
    }
}
