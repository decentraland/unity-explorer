using Arch.Core;
using DCL.CharacterCamera.Components;
using ECS.Abstract;

namespace DCL.CharacterCamera
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<CameraComponent>();

        public static SingleInstanceEntity CacheCamera(this World world) =>
            new (in QUERY, world);

        public static ref CameraComponent GetCameraComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<CameraComponent>(instance);

        public static ref CameraFieldOfViewComponent GetCameraFovComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<CameraFieldOfViewComponent>(instance);
    }
}
