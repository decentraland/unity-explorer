using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.PlayerMarker
{
    public partial class PlayerMarkerController : MapLayerControllerBase, IMapLayerController<PlayerMarkerParameter>, IZoomScalingLayer
    {
        internal delegate IPlayerMarker PlayerMarkerBuilder(Transform parent);

        //This value indicates how big the difference of sqr magnitudes between the last Updated
        //position and the new position must be for a position change event to be triggered
        //This reduces the number of re-calculations done on the path renderer and the minimap pins positions
        private const int MIN_SQR_POSITION_DIFFERENCE_FOR_EVENT_TRIGGER = 400;

        private readonly PlayerMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;

        private IPlayerMarker playerMarker;
        private TrackPlayerPositionSystem system;
        private float lastUpdatePositionSqrMagnitude;

        internal PlayerMarkerController(
            PlayerMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapPathEventBus mapPathEventBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.builder = builder;
            this.mapPathEventBus = mapPathEventBus;
        }

        public void Initialize()
        {
            playerMarker = builder(instantiationParent);
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            system = TrackPlayerPositionSystem.InjectToWorld(ref builder);

            system.SetQueryMethod(SetPlayerTransformQuery);
            system.Activate();
        }

        [All(typeof(PlayerComponent))]
        [Query]
        private void SetPlayerTransform(in CharacterTransform transformComponent)
        {
            SetPosition(transformComponent.Transform.position);
            SetRotation(transformComponent.Transform.rotation);
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            playerMarker.SetActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            playerMarker.SetActive(false);
            return UniTask.CompletedTask;
        }

        public void SetParameter(PlayerMarkerParameter param)
        {
            playerMarker?.SetBackgroundVisibility(param.BackgroundIsActive);
        }

        private void SetPosition(Vector3 position)
        {
            Vector2 gridPosition = ParcelMathHelper.WorldToGridPositionUnclamped(position);
            Vector3 newMarkerPosition = coordsUtils.PivotPosition(playerMarker, coordsUtils.CoordsToPositionWithOffset(gridPosition));

            if (lastUpdatePositionSqrMagnitude == 0 || Mathf.Abs(newMarkerPosition.sqrMagnitude - lastUpdatePositionSqrMagnitude) > MIN_SQR_POSITION_DIFFERENCE_FOR_EVENT_TRIGGER)
            {
                mapPathEventBus.PathUpdated(newMarkerPosition);
                lastUpdatePositionSqrMagnitude = newMarkerPosition.sqrMagnitude;
            }

            playerMarker.SetPosition(newMarkerPosition);
        }

        private void SetRotation(Quaternion rotation)
        {
            playerMarker.SetRotation(Quaternion.AngleAxis(rotation.eulerAngles.y, Vector3.back));
        }

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            playerMarker.SetZoom(baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            playerMarker.ResetToBaseScale();
        }

        protected override void DisposeImpl()
        {
            playerMarker?.Dispose();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayerPositionSystem(World world) : base(world) { }
    }
}
