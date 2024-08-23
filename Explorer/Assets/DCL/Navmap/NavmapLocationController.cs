using Arch.Core;
using DCL.Character.Components;
using DCL.MapRenderer.MapCameraController;
using Utility;

namespace DCL.Navmap
{
    public class NavmapLocationController
    {
        private const float TRANSITION_TIME = 0.5f;
        private const float IMMEDIATE_TRANSITION = 0;

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
            CenterToPlayer(IMMEDIATE_TRANSITION);
        }

        private void CenterToPlayer()
        {
            CenterToPlayer(TRANSITION_TIME);
        }

        private void CenterToPlayer(float duration)
        {
            if (world.TryGet(playerEntity, out playerTransformComponent))
                cameraController.TranslateTo(ParcelMathHelper.WorldToGridPositionUnclamped(playerTransformComponent.Transform.position), duration);
        }
    }
}
