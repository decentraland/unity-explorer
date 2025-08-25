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
        [All(typeof(NametagElement))]
        private void AddIndicator(Entity entity) =>
            World.Add(entity, new DebugRoomIndicatorComponent());

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateIndicator(in AvatarShapeComponent avatarShapeComponent, NametagElement nametagView, ref DebugRoomIndicatorComponent indicatorComponent)
        {
            RoomSource prevValue = indicatorComponent.ConnectedTo;

            indicatorComponent.ConnectedTo = entityParticipantTable.TryGet(avatarShapeComponent.ID, out IReadOnlyEntityParticipantTable.Entry entry) ? entry.ConnectedTo : RoomSource.NONE;

            if (prevValue != indicatorComponent.ConnectedTo)
                nametagView.DebugText = indicatorComponent.ConnectedTo.ToString();
        }

        [Query]
        [None(typeof(NametagElement), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
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
        private void RemoveAllIndicators(Entity entity, in NametagElement nametagElement) =>
            RemoveIndicatorInternal(entity, nametagElement);

        private void RemoveIndicatorInternal(Entity entity, in NametagElement? nametagElement)
        {
            if (nametagElement != null)
                nametagElement.DebugText = null;

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
