using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.SDKComponents.MapPins.Components;
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

        private readonly Dictionary<Entity, IPinMarker> markers = new ();

        private MapPinPlacementSystem system;
        private MapPinTextureResolverSystem textureResolverSystem;
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

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            world = builder.World;
            system = MapPinPlacementSystem.InjectToWorld(ref builder);
            textureResolverSystem = MapPinTextureResolverSystem.InjectToWorld(ref builder);
            mapPinDeletionSystem = MapPinDeletionSystem.InjectToWorld(ref builder);

            system.SetQueryMethod(SetMapPinPlacementQuery);
            textureResolverSystem.SetQueryMethod(SetMapPinTextureQuery);
            mapPinDeletionSystem.SetQueryMethod(HandleEntityDestructionQuery);
            system.Activate();
            textureResolverSystem.Activate();
            mapPinDeletionSystem.Activate();
        }

        [All(typeof(MapPinComponent))]
        [Query]
        private void SetMapPinPlacement(in Entity e, ref MapPinComponent mapPinComponent)
        {
            if (!mapPinComponent.IsDirty)
                return;

            IPinMarker marker;
            if (markers.TryGetValue(e, out IPinMarker pinMarker))
            {
                marker = pinMarker;
            }
            else
            {
                marker = builder(objectsPool, mapCullingController);
                markers.Add(e, marker);
            }
            marker.SetPosition(coordsUtils.CoordsToPositionWithOffset(mapPinComponent.Position));

            if (isEnabled)
                mapCullingController.StartTracking(marker, this);

            mapPinComponent.IsDirty = false;
        }

        [All(typeof(MapPinComponent))]
        [Query]
        private void SetMapPinTexture(in Entity e, ref MapPinComponent mapPinComponent)
        {
            if(mapPinComponent.TexturePromise is null or { IsConsumed: true })
                return;

            IPinMarker marker;
            if (markers.TryGetValue(e, out IPinMarker pinMarker))
            {
                marker = pinMarker;
            }
            else
            {
                marker = builder(objectsPool, mapCullingController);
                markers.Add(e, marker);
            }
            if (mapPinComponent.TexturePromise.Value.TryConsume(world, out StreamableLoadingResult<Texture2D> texture))
                marker.SetTexture(texture.Asset);
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
    public partial class MapPinTextureResolverSystem : ControllerECSBridgeSystem
    {
        internal MapPinTextureResolverSystem(World world) : base(world) { }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MapPinDeletionSystem : ControllerECSBridgeSystem
    {
        internal MapPinDeletionSystem(World world) : base(world) { }
    }
}
