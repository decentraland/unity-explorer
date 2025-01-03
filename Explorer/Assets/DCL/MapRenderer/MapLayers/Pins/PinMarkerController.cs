using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.MapPins.Bus;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal class PinMarkerController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        public readonly Dictionary<Entity, IPinMarker> markers = new ();

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly IMapPinsEventBus mapPinsEventBus;

        private MapPinBridgeSystem system;

        private bool isEnabled;

        public PinMarkerController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.mapPathEventBus = mapPathEventBus;
            this.mapPinsEventBus = mapPinsEventBus;
            this.mapPinsEventBus.OnUpdateMapPin += SetOrUpdateMapPinPlacement;
            this.mapPinsEventBus.OnRemoveMapPin += RemoveMapPin;
            this.mapPinsEventBus.OnUpdateMapPinThumbnail += SetOrUpdateMapPinThumbnail;
            this.mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder) { }

        private void OnRemovedDestination()
        {
            foreach (KeyValuePair<Entity, IPinMarker> pair in markers)
            {
                if (pair.Value.IsDestination)
                {
                    pair.Value.SetAsDestination(false);
                    break;
                }
            }
        }

        private void SetOrUpdateMapPinPlacement(Entity entity, Vector2Int position, string title, string description)
        {
            IPinMarker marker;

            if (!markers.TryGetValue(entity, out IPinMarker pinMarker))
            {
                marker = builder(objectsPool, mapCullingController);
                markers.Add(entity, marker);
            }
            else { marker = pinMarker; }

            marker.SetPosition(coordsUtils.CoordsToPositionWithOffset(position), position);
            marker.SetData(title, description);

            if (isEnabled)
                mapCullingController.StartTracking(marker, this);
        }

        private void RemoveMapPin(Entity entity)
        {
            if (markers.TryGetValue(entity, out IPinMarker marker))
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
                markers.Remove(entity);
            }
        }

        private void SetOrUpdateMapPinThumbnail(Entity entity, Texture2D thumbnail)
        {
            IPinMarker marker;

            if (!markers.TryGetValue(entity, out IPinMarker pinMarker))
            {
                marker = builder(objectsPool, mapCullingController);
                markers.Add(entity, marker);
            }
            else marker = pinMarker;

            marker.SetTexture(thumbnail);
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (IPinMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();

            mapPinsEventBus.OnUpdateMapPin -= SetOrUpdateMapPinPlacement;
            mapPinsEventBus.OnRemoveMapPin -= RemoveMapPin;
            mapPinsEventBus.OnUpdateMapPinThumbnail -= SetOrUpdateMapPinThumbnail;
            mapPathEventBus.OnRemovedDestination -= OnRemovedDestination;
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
                marker.ResetScale(IPinMarker.ScaleType.MINIMAP);
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
