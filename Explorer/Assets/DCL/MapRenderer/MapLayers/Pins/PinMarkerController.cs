using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapPins.Components;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal partial class PinMarkerController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly PinMarkerBuilder builder;

        public readonly Dictionary<Entity, IPinMarker> markers = new ();

        private MapPinPlacementSystem system;
        private MapPinDeletionSystem mapPinDeletionSystem;
        private World world;

        private bool isEnabled;

        public PinMarkerController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            world = builder.World;
            system = MapPinPlacementSystem.InjectToWorld(ref builder);
            mapPinDeletionSystem = MapPinDeletionSystem.InjectToWorld(ref builder);

            system.SetQueryMethod(SetMapPinPlacementQuery);
            mapPinDeletionSystem.SetQueryMethod(HandleEntityDestructionQuery);
            system.Activate();
            mapPinDeletionSystem.Activate();
        }

        [Query]
        private void SetMapPinPlacement(in Entity e, ref MapPinComponent mapPinComponent, ref PBMapPin pbMapPin)
        {
            if (mapPinComponent.IsDirty)
            {
                IPinMarker marker;
                if (!markers.TryGetValue(e, out IPinMarker pinMarker))
                {
                    marker = builder(objectsPool, mapCullingController);
                    markers.Add(e, marker);
                }
                else
                {
                    marker = pinMarker;
                }

                marker.SetPosition(coordsUtils.CoordsToPositionWithOffset(mapPinComponent.Position), mapPinComponent.Position);
                marker.SetData(pbMapPin.Title, pbMapPin.Description);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);

                mapPinComponent.IsDirty = false;
            }

            if (mapPinComponent.TexturePromise is not null && !mapPinComponent.TexturePromise.Value.IsConsumed)
            {
                IPinMarker marker;
                if (!markers.TryGetValue(e, out IPinMarker pinMarker))
                {
                    marker = builder(objectsPool, mapCullingController);
                    markers.Add(e, marker);
                }
                else
                {
                    marker = pinMarker;
                }

                if (mapPinComponent.TexturePromise.Value.TryConsume(world, out StreamableLoadingResult<Texture2D> texture))
                {
                    marker.SetTexture(texture.Asset);
                }
            }
        }

        [All(typeof(DeleteEntityIntention))]
        [Query]
        private void HandleEntityDestruction(in Entity e, in PBMapPin pbMapPin)
        {
            if (markers.TryGetValue(e, out IPinMarker marker))
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
                markers.Remove(e);
            }
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (IPinMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(IPinMarker marker)
        {
            marker.OnBecameVisible();
        }

        public void OnMapObjectCulled(IPinMarker marker)
        {
            marker.OnBecameInvisible();
        }

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            foreach (IPinMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (IPinMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (IPinMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            return UniTask.CompletedTask;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MapPinPlacementSystem : ControllerECSBridgeSystem
    {
        internal MapPinPlacementSystem(World world) : base(world) { }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MapPinDeletionSystem : ControllerECSBridgeSystem
    {
        internal MapPinDeletionSystem(World world) : base(world) { }
    }
}
