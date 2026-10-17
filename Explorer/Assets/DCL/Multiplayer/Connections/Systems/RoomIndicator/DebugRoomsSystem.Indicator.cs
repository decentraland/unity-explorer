using Arch.Core;
using Arch.System;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.LifeCycle.Components;

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
            RoomSource announced = entityParticipantTable.TryGet(avatarShapeComponent.ID, out IReadOnlyEntityParticipantTable.Entry entry) ? entry.ConnectedTo : RoomSource.None;
            RoomSource present = roomHub.RoomsOf(avatarShapeComponent.ID);

            if (announced == indicatorComponent.Announced && present == indicatorComponent.Present)
                return;

            indicatorComponent.Announced = announced;
            indicatorComponent.Present = present;

            nametagHolder.Nametag.DebugText = RoomIndicatorLabel.Build(announced, present);
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

                // An avatar out of nametag range has no holder, so the query above cannot reach it; without this its
                // component would survive the toggle and its stale state would suppress the first write on the way back.
                RemoveIndicatorOnComponentRemovalQuery(World);
                return;
            }

            RemoveIndicatorOnEntityRemovalQuery(World);
            RemoveIndicatorOnComponentRemovalQuery(World);
            AddIndicatorQuery(World);
            UpdateIndicatorQuery(World);
        }
    }
}
