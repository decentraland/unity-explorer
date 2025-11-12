using Arch.Core;
using DCL.Character.Components;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Navmap.FilterPanel;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapLocationController
    {
        private const float TRANSITION_TIME = 0.5f;

        private IMapCameraController cameraController;
        private CharacterTransform playerTransformComponent;

        private readonly NavmapLocationView view;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly NavmapFilterPanelController navmapFilterPanelController;
        private readonly HomePlaceEventBus homePlaceEventBus;

        public NavmapLocationController(
            NavmapLocationView view,
            World world,
            Entity playerEntity,
            NavmapFilterPanelController navmapFilterPanelController,
            INavmapBus navmapBus,
            HomePlaceEventBus homePlaceEventBus)
        {
            this.view = view;
            this.world = world;
            this.playerEntity = playerEntity;
            this.navmapFilterPanelController = navmapFilterPanelController;
            this.homePlaceEventBus = homePlaceEventBus;
            world.TryGet(playerEntity, out playerTransformComponent);

            this.view.CenterToHomeButton.onClick.AddListener(CenterToHome);
            this.view.CenterToPlayerButton.onClick.AddListener(CenterToPlayer);
            this.view.FilterPanelButton.onClick.AddListener(ToggleFilterPanel);

            navmapBus.OnMoveCameraTo += MoveCameraTo;
        }

        private void MoveCameraTo(Vector2 destination, float speed = 0)
        {
            cameraController.TranslateTo(destination, speed == 0 ? TRANSITION_TIME : speed);
        }

        private void ToggleFilterPanel()
        {
            navmapFilterPanelController.ToggleFilterPanel();
        }

        public void InjectCameraController(IMapCameraController controller)
        {
            this.cameraController = controller;

            if (TryGetPlayerCoordinates(out var coordinates))
                cameraController.SetPosition(coordinates);
        }

        private void CenterToHome()
        {
            cameraController.TranslateTo(TryGetHomeCoordinates(out var coordinates) 
                ? coordinates 
                : Vector2Int.zero, TRANSITION_TIME);
        }

        private void CenterToPlayer()
        {
            if (TryGetPlayerCoordinates(out var coordinates))
                cameraController.TranslateTo(coordinates, TRANSITION_TIME);
        }
        
        private bool TryGetHomeCoordinates(out Vector2Int coordinates)
        {
            return homePlaceEventBus.TryGetHomeCoordinates(out coordinates);
        }

        private bool TryGetPlayerCoordinates(out Vector2 coordinates)
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
