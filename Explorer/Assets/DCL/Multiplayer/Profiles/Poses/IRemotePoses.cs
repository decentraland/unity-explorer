using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public interface IRemotePoses
    {
        Vector2Int ParcelPose(string walletId);

        void BroadcastSelfPose(Vector2Int pose);
    }
}
