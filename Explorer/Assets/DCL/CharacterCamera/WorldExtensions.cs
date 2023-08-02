using Arch.Core;
using ECS.Abstract;

namespace DCL.CharacterCamera
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<CameraComponent>();

        public static SingleInstanceEntity CacheCamera(this World world) =>
            new (in QUERY, world);

        public static ref readonly CameraComponent GetCameraComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<CameraComponent>(instance);
    }
}
