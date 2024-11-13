using Arch.Core;
using DCL.Character.Components;
using DCL.MapRenderer.MapCameraController;
using DCL.Navmap.FilterPanel;
using System;
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
        private readonly NavmapFilterPanelController navmapFilterPanelController;

        public NavmapLocationController(NavmapLocationView view, World world, Entity playerEntity, NavmapFilterPanelController navmapFilterPanelController)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.navmapFilterPanelController = navmapFilterPanelController;
            world.TryGet(playerEntity, out playerTransformComponent);

            view.CenterToPlayerButton.onClick.AddListener(CenterToPlayer);
            view.FilterPanelButton.onClick.AddListener(ToggleFilterPanel);
        }

        private void ToggleFilterPanel()
        {
            navmapFilterPanelController.ToggleFilterPanel();
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
