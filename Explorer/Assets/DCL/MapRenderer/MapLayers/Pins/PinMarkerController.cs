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

        public readonly Dictionary<Entity, IPinMarker> markers = new ();

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;

        private MapPinBridgeSystem system;

        private bool isEnabled;

        public PinMarkerController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapPathEventBus mapPathEventBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.mapPathEventBus = mapPathEventBus;
            this.mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            system = MapPinBridgeSystem.InjectToWorld(ref builder);

            system.SetQueryMethod((ControllerECSBridgeSystem.QueryMethod)SetMapPinPlacementQuery + HandleEntityDestructionQuery);
            system.Activate();
        }

        private void OnRemovedDestination()
        {
            foreach (KeyValuePair<Entity, IPinMarker> pair in markers) { pair.Value.SetAsDestination(false); }
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
                else { marker = pinMarker; }

                marker.SetPosition(coordsUtils.CoordsToPositionWithOffset(mapPinComponent.Position), mapPinComponent.Position);
                marker.SetData(pbMapPin.Title, pbMapPin.Description);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);

                mapPinComponent.IsDirty = false;
            }

            if (mapPinComponent.ThumbnailIsDirty)
            {
                IPinMarker marker;

                if (!markers.TryGetValue(e, out IPinMarker pinMarker))
                {
                    marker = builder(objectsPool, mapCullingController);
                    markers.Add(e, marker);
                }
                else { marker = pinMarker; }

                marker.SetTexture(mapPinComponent.Thumbnail);
                mapPinComponent.ThumbnailIsDirty = false;
            }
        }

        [All(typeof(DeleteEntityIntention), typeof(PBMapPin))]
        [Query]
        private void HandleEntityDestruction(in Entity e)
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
            foreach (IPinMarker marker in markers.Values)
                marker.ResetScale();
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
    public partial class MapPinBridgeSystem : ControllerECSBridgeSystem
    {
        internal MapPinBridgeSystem(World world) : base(world) { }
    }
}
