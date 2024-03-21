using DCL.Character;
using System;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Profiles.Poses
{
    public interface IRemotePoses
    {
        Vector2Int ParcelPose(string walletId);

        void BroadcastSelfPose(Vector2Int pose);

        class Fake : IRemotePoses
        {
            public Vector2Int ParcelPose(string walletId) =>
                Vector2Int.zero;

            public void BroadcastSelfPose(Vector2Int pose)
            {
                //ignore
            }
        }
    }

    public static class RemotePosesExtensions
    {
        public static void BroadcastSelfPose(this IRemotePoses remotePoses, ICharacterObject characterObject)
        {
            var playerPosition = characterObject.Position;
            Vector2Int playerParcelPosition = ParcelMathHelper.WorldToGridPosition(playerPosition);
            remotePoses.BroadcastSelfPose(playerParcelPosition);
        }
    }
}
