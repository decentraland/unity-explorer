using Arch.Core;
using DCL.Character.Components;
using DCL.MapRenderer.MapCameraController;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapLocationController
    {
        private const float TRANSITION_TIME = 0.5f;

        private IMapCameraController cameraController;
        private CharacterTransform playerTransformComponent;

        private readonly World world;
        private readonly Entity playerEntity;

        public NavmapLocationController(NavmapLocationView view, World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            world.TryGet(playerEntity, out playerTransformComponent);

            view.CenterToPlayerButton.onClick.AddListener(CenterToPlayer);
        }

        public void InjectCameraController(IMapCameraController controller)
        {
            this.cameraController = controller;

            if (TryGetCoordinates(out var coordinates))
                cameraController.SetPosition(coordinates);
        }

        private void CenterToPlayer()
        {
            if (TryGetCoordinates(out var coordinates))
                cameraController.TranslateTo(coordinates, TRANSITION_TIME);
        }

        private bool TryGetCoordinates(out Vector2 coordinates)
        {
            if (world.TryGet(playerEntity, out playerTransformComponent))
            {
                coordinates = ParcelMathHelper.WorldToGridPositionUnclamped(playerTransformComponent.Transform.position);
                return true;
            }

            coordinates = Vector2.zero;
            return false;
        }
    }
}
