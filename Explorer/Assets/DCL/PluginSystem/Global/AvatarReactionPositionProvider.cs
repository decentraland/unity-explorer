using Arch.Core;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Multiplayer.Connections.Typing;
using DCL.Multiplayer.Profiles.Tables;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Resolves avatar head positions from the ECS world using the same API
    /// as the nametag system (<see cref="AvatarBase.GetAdaptiveNametagPosition"/>).
    /// Lives in DCL.Plugins because it uses FindAvatarUtils (AvatarShape/Systems is asmref'd into Plugins).
    /// </summary>
    public sealed class AvatarReactionPositionProvider : IAvatarReactionPosition
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly List<Vector3> nearbyPositionsCache = new (32);

        public AvatarReactionPositionProvider(
            World world,
            Entity playerEntity,
            IReadOnlyEntityParticipantTable entityParticipantTable)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.entityParticipantTable = entityParticipantTable;
        }

        public Vector3? GetLocalPlayerHeadPosition()
        {
            if (!world.IsAlive(playerEntity))
                return null;

            if (!world.TryGet(playerEntity, out AvatarBase avatarBase) || avatarBase == null)
                return null;

            return avatarBase.GetAdaptiveNametagPosition();
        }

        public Vector3? GetHeadPosition(string walletId)
        {
            if (string.IsNullOrEmpty(walletId))
                return null;

            var result = FindAvatarUtils.AvatarWithID(world, walletId, entityParticipantTable);

            if (!result.Success)
                return null;

            return result.Result.GetAdaptiveNametagPosition();
        }

        public List<Vector3> GetAllNearbyHeadPositions()
        {
            Profiler.BeginSample("ChatReactions.GetNearbyHeads");
            nearbyPositionsCache.Clear();

            foreach (string walletId in entityParticipantTable.Wallets())
            {
                var entry = entityParticipantTable.Get(walletId);

                if (!world.IsAlive(entry.Entity))
                    continue;

                if (!world.TryGet(entry.Entity, out AvatarBase avatarBase) || avatarBase == null)
                    continue;

                nearbyPositionsCache.Add(avatarBase.GetAdaptiveNametagPosition());
            }

            Profiler.EndSample();
            return nearbyPositionsCache;
        }
    }
}
