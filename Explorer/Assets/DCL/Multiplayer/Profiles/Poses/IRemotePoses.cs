using DCL.Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Profiles.Poses
{
    public interface IRemotePoses : IReadOnlyCollection<KeyValuePair<string, Vector2Int>>
    {
        Vector2Int ParcelPose(string walletId);

        void BroadcastSelfPose(Vector2Int pose);

        class Fake : IRemotePoses
        {
            public int Count => 0;

            public Vector2Int ParcelPose(string walletId) =>
                Vector2Int.zero;

            public void BroadcastSelfPose(Vector2Int pose)
            {
                //ignore
            }

            public IEnumerator<KeyValuePair<string, Vector2Int>> GetEnumerator()
            {
                yield break;
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
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
