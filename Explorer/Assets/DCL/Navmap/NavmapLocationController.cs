using Arch.Core;
using DCL.Character.Components;
using DCL.MapRenderer.MapCameraController;
using Utility;

namespace DCL.Navmap
{
    public class NavmapLocationController
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private const float TRANSITION_TIME = 0.5f;

        private IMapCameraController cameraController;
        private CharacterTransform playerTransformComponent;

        public NavmapLocationController(NavmapLocationView view, World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            world.TryGet(playerEntity, out playerTransformComponent);

            view.CenterToPlayerButton.onClick.AddListener(OnCenterToPlayer);
        }

        public void InjectCameraController(IMapCameraController controller)
        {
            this.cameraController = controller;
        }

        private void OnCenterToPlayer()
        {
            if(world.TryGet(playerEntity, out playerTransformComponent))
                cameraController.TranslateTo(ParcelMathHelper.WorldToGridPositionUnclamped(playerTransformComponent.Transform.position),TRANSITION_TIME);
        }
    }
}
