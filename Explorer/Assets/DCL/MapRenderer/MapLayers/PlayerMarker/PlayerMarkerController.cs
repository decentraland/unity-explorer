using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCLServices.MapRenderer.CoordsUtils;
using DCLServices.MapRenderer.Culling;
using ECS.Unity.Transforms.Components;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCLServices.MapRenderer.MapLayers.PlayerMarker
{
    public partial class PlayerMarkerController : MapLayerControllerBase, IMapLayerController<PlayerMarkerParameter>, IZoomScalingLayer
    {
        internal delegate IPlayerMarker PlayerMarkerBuilder(Transform parent);

        private readonly PlayerMarkerBuilder builder;

        private IPlayerMarker playerMarker;
        private TrackPlayerPositionSystem system;

        internal PlayerMarkerController(
            PlayerMarkerBuilder builder,
            Transform instantiationParent, ICoordsUtils coordsUtils, IMapCullingController cullingController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.builder = builder;
        }

        public void Initialize()
        {
            playerMarker = builder(instantiationParent);
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            system = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            system.SetQueryMethod(SetPlayerTransformQuery);
        }

        [All(typeof(PlayerComponent))]
        [Query]
        private void SetPlayerTransform(in TransformComponent transformComponent)
        {
            var position = transformComponent.Transform.position;
            var rotation = transformComponent.Transform.rotation.eulerAngles;
            SetPosition(position);
            SetRotation(rotation);
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            playerMarker.SetActive(true);
            system.Activate();
            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            playerMarker.SetActive(false);
            system.Deactivate();
            return UniTask.CompletedTask;
        }

        public void SetParameter(PlayerMarkerParameter param)
        {
            playerMarker?.SetBackgroundVisibility(param.BackgroundIsActive);
        }

        private void SetPosition(Vector3 position)
        {
            var gridPosition = ParcelMathHelper.WorldToGridPositionUnclamped(position);
            playerMarker.SetPosition(coordsUtils.PivotPosition(playerMarker, coordsUtils.CoordsToPositionWithOffset(gridPosition)));
        }

        private void SetRotation(Vector3 rotation)
        {
            var markerRot = Quaternion.Euler(0, 0, Mathf.Atan2(-rotation.x, rotation.z) * Mathf.Rad2Deg);
            playerMarker.SetRotation(markerRot);
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
