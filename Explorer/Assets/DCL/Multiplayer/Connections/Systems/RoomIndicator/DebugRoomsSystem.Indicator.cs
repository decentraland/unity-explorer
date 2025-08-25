using Arch.Core;
using Arch.System;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;

// ReSharper disable once CheckNamespace
namespace DCL.Multiplayer.Connections.Systems
{
    public partial class DebugRoomsSystem
    {
        [Query]
        [None(typeof(DebugRoomIndicatorComponent), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        [All(typeof(NametagHolder))]
        private void AddIndicator(Entity entity) =>
            World.Add(entity, new DebugRoomIndicatorComponent());

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateIndicator(in AvatarShapeComponent avatarShapeComponent, NametagHolder nametagHolder, ref DebugRoomIndicatorComponent indicatorComponent)
        {
            RoomSource prevValue = indicatorComponent.ConnectedTo;

            indicatorComponent.ConnectedTo = entityParticipantTable.TryGet(avatarShapeComponent.ID, out IReadOnlyEntityParticipantTable.Entry entry) ? entry.ConnectedTo : RoomSource.NONE;

            if (prevValue != indicatorComponent.ConnectedTo)
                nametagHolder.Nametag.DebugText = indicatorComponent.ConnectedTo.ToString();
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        [All(typeof(DebugRoomIndicatorComponent))]
        private void RemoveIndicatorOnComponentRemoval(Entity entity) =>
            RemoveIndicatorInternal(entity, null);

        [Query]
        [All(typeof(DebugRoomIndicatorComponent))]
        private void RemoveIndicatorOnEntityRemoval(Entity entity, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                RemoveIndicatorInternal(entity, null);
        }

        [Query]
        [All(typeof(DebugRoomIndicatorComponent))]
        private void RemoveAllIndicators(Entity entity, in NametagHolder nametagHolder) =>
            RemoveIndicatorInternal(entity, nametagHolder);

        private void RemoveIndicatorInternal(Entity entity, in NametagHolder? nametagHolder)
        {
            if (nametagHolder != null)
                nametagHolder.Nametag.DebugText = null;

            World.Remove<DebugRoomIndicatorComponent>(entity);
        }

        partial void UpdateRoomIndicators()
        {
            if (!debugAvatarsRooms.Value)
            {
                RemoveAllIndicatorsQuery(World);
                return;
            }

            RemoveIndicatorOnEntityRemovalQuery(World);
            RemoveIndicatorOnComponentRemovalQuery(World);
            AddIndicatorQuery(World);
            UpdateIndicatorQuery(World);
        }
    }
}
