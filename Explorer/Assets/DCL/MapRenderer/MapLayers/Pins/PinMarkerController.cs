using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.MapPins.Bus;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using MVC;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal class PinMarkerController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        public bool ZoomBlocked { get; set; }

        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        public readonly Dictionary<Entity, IPinMarker> markers = new ();

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly Dictionary<GameObject, IPinMarker> visibleMarkers = new ();
        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly IMapPinsEventBus mapPinsEventBus;
        private readonly INavmapBus navmapBus;

        private bool isEnabled;
        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private IPinMarker? previousMarker;

        public PinMarkerController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.mapPathEventBus = mapPathEventBus;
            this.mapPinsEventBus = mapPinsEventBus;
            this.mapPinsEventBus.OnUpdateMapPin += SetOrUpdateMapPinPlacement;
            this.mapPinsEventBus.OnRemoveMapPin += RemoveMapPin;
            this.mapPinsEventBus.OnUpdateMapPinThumbnail += SetOrUpdateMapPinThumbnail;
            this.navmapBus = navmapBus;
            this.mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

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
            GameObject? gameObject = marker.GetGameObject();

            if (gameObject != null)
                visibleMarkers.AddOrReplace(gameObject, marker);
        }

        public void OnMapObjectCulled(IPinMarker marker)
        {
            GameObject? gameObject = marker.GetGameObject();

            if (gameObject != null)
                visibleMarkers.Remove(gameObject);

            marker.OnBecameInvisible();
        }

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            if (ZoomBlocked)
                return;

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

        public UniTask EnableAsync(CancellationToken cancellationToken)
        {
            foreach (IPinMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            return UniTask.CompletedTask;
        }

        public bool TryHighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
        {
            mapMarker = null;
            if (visibleMarkers.TryGetValue(gameObject, out IPinMarker marker))
            {
                mapMarker = marker;
                highlightCt = highlightCt.SafeRestart();
                previousMarker?.AnimateSelectionAsync(deHighlightCt.Token);
                marker.AnimateSelectionAsync(highlightCt.Token);
                previousMarker = marker;
                return true;
            }

            return false;
        }

        public bool TryDeHighlightObject(GameObject gameObject)
        {
            previousMarker = null;

            if (visibleMarkers.TryGetValue(gameObject, out IPinMarker marker))
            {
                deHighlightCt = deHighlightCt.SafeRestart();
                marker.AnimateDeselectionAsync(deHighlightCt.Token);
                return true;
            }

            return false;
        }

        public bool TryClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRendererMarker)
        {
            mapRendererMarker = null;
            if (visibleMarkers.TryGetValue(gameObject, out IPinMarker marker))
            {
                navmapBus.SelectPlaceAsync(marker.ParcelPosition, cts.Token).Forget();
                mapRendererMarker = marker;
                return true;
            }

            return false;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MapPinBridgeSystem : ControllerECSBridgeSystem
    {
        internal MapPinBridgeSystem(World world) : base(world) { }
    }
}
