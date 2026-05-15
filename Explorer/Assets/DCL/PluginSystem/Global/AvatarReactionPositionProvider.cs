using Arch.Core;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Chat.ChatReactions.Configs;
using DCL.Multiplayer.Profiles.Tables;
using UnityEngine;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine.Profiling;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    /// Resolves avatar head positions from the ECS world using the same API
    /// as the nametag system (<see cref="AvatarBase.GetAdaptiveNametagPosition"/>).
    /// Lives in DCL.Plugins because it uses FindAvatarUtils (AvatarShape/Systems is asmref'd into Plugins).
    /// </summary>
    public sealed class AvatarReactionPositionProvider : IAvatarReactionPosition
    {
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ChatReactionsWorldLaneConfig worldLaneConfig;
        private readonly List<Vector3> nearbyPositionsCache = new (32);

        public int LastNearbyCount => nearbyPositionsCache.Count;

        public AvatarReactionPositionProvider(
            Arch.Core.World world,
            Entity playerEntity,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ChatReactionsWorldLaneConfig worldLaneConfig)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.entityParticipantTable = entityParticipantTable;
            this.worldLaneConfig = worldLaneConfig;
        }

        public Vector3? GetLocalPlayerHeadPosition()
        {
            if (!world.IsAlive(playerEntity))
                return null;

            if (!world.TryGet(playerEntity, out AvatarBase avatarBase) || avatarBase == null)
                return null;

            return avatarBase.GetAdaptiveNametagPosition() + worldLaneConfig.AnchorOffset;
        }

        public Vector3? GetHeadPosition(string walletId)
        {
            if (string.IsNullOrEmpty(walletId))
                return null;

            var result = FindAvatarUtils.AvatarWithID(world, walletId, entityParticipantTable);

            if (!result.Success)
                return null;

            return result.Result.GetAdaptiveNametagPosition() + worldLaneConfig.AnchorOffset;
        }

        public List<Vector3> GetAllNearbyHeadPositions()
        {
            Profiler.BeginSample("ChatReactions.GetNearbyHeads");
            nearbyPositionsCache.Clear();

            Vector3 offset = worldLaneConfig.AnchorOffset;

            foreach (string walletId in entityParticipantTable.Wallets())
            {
                if (!entityParticipantTable.TryGet(walletId, out var entry))
                    continue;

                if (!world.IsAlive(entry.Entity))
                    continue;

                if (!world.TryGet(entry.Entity, out AvatarBase avatarBase) || avatarBase == null)
                    continue;

                nearbyPositionsCache.Add(avatarBase.GetAdaptiveNametagPosition() + offset);
            }

            Profiler.EndSample();
            return nearbyPositionsCache;
        }
    }
}
