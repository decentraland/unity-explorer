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
            playerMarker.SetPosition(coordsUtils.PivotPosition(playerMarker, coordsUtils.CoordsToPositionWithOffset(gridPosition)));
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
