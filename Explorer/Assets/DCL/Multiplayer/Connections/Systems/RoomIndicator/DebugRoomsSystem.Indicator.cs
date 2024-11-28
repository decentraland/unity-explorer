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
        private readonly IObjectPool<DebugRoomIndicatorView> roomIndicatorPool;

        [Query]
        [None(typeof(DebugRoomIndicatorComponent), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void AddIndicator(Entity entity, NametagView nametagView)
        {
            DebugRoomIndicatorView? view = roomIndicatorPool.Get();
            view.Attach(nametagView.Background);
            World.Add(entity, new DebugRoomIndicatorComponent(view));
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateIndicator(in AvatarShapeComponent avatarShapeComponent, NametagView nametagView, ref DebugRoomIndicatorComponent indicatorComponent)
        {
            RoomSource prevValue = indicatorComponent.ConnectedTo;

            indicatorComponent.ConnectedTo = entityParticipantTable.TryGet(avatarShapeComponent.ID, out IReadOnlyEntityParticipantTable.Entry entry) ? entry.ConnectedTo : RoomSource.NONE;

            if (prevValue != indicatorComponent.ConnectedTo) { indicatorComponent.View.SetRooms(indicatorComponent.ConnectedTo); }

            indicatorComponent.View.UpdateTransparency(nametagView.alpha);
        }

        [Query]
        [None(typeof(NametagView), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void RemoveIndicatorOnComponentRemoval(Entity entity, in DebugRoomIndicatorComponent component)
        {
            RemoveIndicatorInternal(entity, component);
        }

        [Query]
        private void RemoveIndicatorOnEntityRemoval(Entity entity, in DebugRoomIndicatorComponent component, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion == false)
                RemoveIndicatorInternal(entity, component);
        }

        [Query]
        private void RemoveAllIndicators(Entity entity, in DebugRoomIndicatorComponent component)
        {
            RemoveIndicatorInternal(entity, component);
        }

        private void RemoveIndicatorInternal(Entity entity, in DebugRoomIndicatorComponent component)
        {
            roomIndicatorPool.Release(component.View);
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
