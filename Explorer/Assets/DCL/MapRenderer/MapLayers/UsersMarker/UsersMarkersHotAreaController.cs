using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterPreview.Components;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using ECS.LifeCycle.Components;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Users
{
    internal partial class UsersMarkersHotAreaController : MapLayerControllerBase, IMapLayerController
    {
        private readonly IObjectPool<HotUserMarkerObject> objectsPool;
        private readonly IObjectPool<IHotUserMarker> wrapsPool;
        private TrackPlayersPositionSystem trackSystem;
        private RemovedTrackedPlayersPositionSystem untrackSystem;

        private readonly Dictionary<string, IHotUserMarker> markers = new ();
        private bool isEnabled;

        public UsersMarkersHotAreaController(
            IObjectPool<HotUserMarkerObject> objectsPool,
            IObjectPool<IHotUserMarker> wrapsPool,
            Transform parent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController)
            : base(parent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.wrapsPool = wrapsPool;
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();
            wrapsPool.Clear();
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            trackSystem = TrackPlayersPositionSystem.InjectToWorld(ref builder);
            trackSystem.SetQueryMethod(SetPlayerMarkerQuery);
            untrackSystem = RemovedTrackedPlayersPositionSystem.InjectToWorld(ref builder);
            untrackSystem.SetQueryMethod(RemoveMarkerQuery);
            trackSystem.Activate();
            untrackSystem.Activate();
        }

        [All(typeof(CharacterTransform))]
        [None(typeof(PlayerComponent), typeof(CharacterPreviewComponent))]
        [Query]
        private void SetPlayerMarker(in CharacterTransform transformComponent, in AvatarShapeComponent avatarShape)
        {
            if (!isEnabled)
                return;

            if(markers.TryGetValue(avatarShape.ID, out var marker))
            {
                marker.UpdateMarkerPosition(avatarShape.ID, transformComponent.Transform.position);
                mapCullingController.SetTrackedObjectPositionDirty(marker);
            }
            else
            {
                var wrap = wrapsPool.Get();
                markers.Add(avatarShape.ID, wrap);
                mapCullingController.StartTracking(wrap, wrap);
            }
        }

        [Query]
        private void RemoveMarker(in AvatarShapeComponent avatarShape, in DeleteEntityIntention deleteEntityIntention)
        {
            if (markers.TryGetValue(avatarShape.ID, out var marker))
            {
                mapCullingController.StopTracking(marker);
                wrapsPool.Release(marker);
                markers.Remove(avatarShape.ID);
            }
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            isEnabled = true;
            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            isEnabled = false;

            foreach (IHotUserMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                wrapsPool.Release(marker);
            }

            wrapsPool.Clear();
            markers.Clear();
            return UniTask.CompletedTask;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayersPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayersPositionSystem(World world) : base(world) { }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RemovedTrackedPlayersPositionSystem : ControllerECSBridgeSystem
    {
        internal RemovedTrackedPlayersPositionSystem(World world) : base(world) { }
    }
}
