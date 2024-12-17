using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.MapPins.Components;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using ECS.LifeCycle.Components;
using MVC;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal partial class PinMarkerController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        public readonly Dictionary<Entity, IPinMarker> markers = new ();

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly Dictionary<GameObject, IPinMarker> visibleMarkers = new ();
        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly INavmapBus navmapBus;

        private MapPinBridgeSystem? system;
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
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.mapPathEventBus = mapPathEventBus;
            this.navmapBus = navmapBus;
            this.mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            system = MapPinBridgeSystem.InjectToWorld(ref builder);

            system.SetQueryMethod((ControllerECSBridgeSystem.QueryMethod)SetMapPinPlacementQuery + HandleEntityDestructionQuery);
            system.Activate();
        }

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

        public bool HighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
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

        public bool DeHighlightObject(GameObject gameObject)
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

        public bool ClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRendererMarker)
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
