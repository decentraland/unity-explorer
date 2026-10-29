using Arch.Core;
using CRDT;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    internal readonly struct ScenePlayerAvatarResolver
    {
        public enum Result
        {
            Resolved,

            /// <summary>The avatar behind the scene entity does not exist yet, but still can.</summary>
            Pending,

            /// <summary>No avatar backs the scene entity, and none ever will.</summary>
            Unreachable,

            /// <summary>Only a scan over the player entities can tell which of the other outcomes applies.</summary>
            NeedsPlayerScan,
        }

        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;

        public ScenePlayerAvatarResolver(World globalWorld, Entity globalPlayerEntity, IReadOnlyEntityParticipantTable entityParticipantTable)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.entityParticipantTable = entityParticipantTable;
        }

        public Result ResolveLocalPlayer(out Entity target)
        {
            target = globalPlayerEntity;
            return globalWorld.IsAlive(target) ? Result.Resolved : Result.Unreachable;
        }

        /// <summary>
        ///     One player has two scene entities that share nothing but the CRDT id: the CRDT bridge's,
        ///     which the scene writes to, and the multiplayer bridge's, which holds the <see cref="SDKProfile" />.
        /// </summary>
        public Result ResolveRemotePlayer(World sceneWorld, Entity sceneEntity, out Entity target)
        {
            if (sceneWorld.TryGet(sceneEntity, out SDKProfile? ownProfile))
                return ResolveByUserId(ownProfile?.UserId ?? string.Empty, out target);

            target = Entity.Null;
            return Result.NeedsPlayerScan;
        }

        public Result ResolveScannedPlayer(World sceneWorld, in PendingScan scan, out Entity target)
        {
            if (scan.PlayerEntity == Entity.Null)
            {
                target = Entity.Null;
                return Result.Unreachable;
            }

            string userId = sceneWorld.TryGet(scan.PlayerEntity, out SDKProfile? bridgeProfile)
                ? bridgeProfile?.UserId ?? string.Empty
                : string.Empty;

            return ResolveByUserId(userId, out target);
        }

        public static void MatchPlayer(List<PendingScan> pendingScans, Entity playerEntity, in PlayerSceneCRDTEntity playerCrdtEntity)
        {
            for (var i = 0; i < pendingScans.Count; i++)
            {
                if (pendingScans[i].CrdtEntity.Id != playerCrdtEntity.CRDTEntity.Id) continue;

                PendingScan scan = pendingScans[i];
                scan.PlayerEntity = playerEntity;
                pendingScans[i] = scan;
            }
        }

        private Result ResolveByUserId(string userId, out Entity target)
        {
            target = Entity.Null;

            // A player entity without a profile is mid-setup, not gone.
            if (string.IsNullOrEmpty(userId))
                return Result.Pending;

            if (!entityParticipantTable.TryGet(userId, out IReadOnlyEntityParticipantTable.Entry entry)
                || !globalWorld.IsAlive(entry.Entity))
                return Result.Unreachable;

            target = entry.Entity;
            return Result.Resolved;
        }

        public struct PendingScan
        {
            public readonly Entity SceneEntity;
            public readonly CRDTEntity CrdtEntity;
            public Entity PlayerEntity;

            public PendingScan(Entity sceneEntity, CRDTEntity crdtEntity)
            {
                SceneEntity = sceneEntity;
                CrdtEntity = crdtEntity;
                PlayerEntity = Entity.Null;
            }
        }
    }
}
