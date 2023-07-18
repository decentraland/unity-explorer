using Arch.Core;
using CrdtEcsBridge.Components.Special;
using UnityEngine;

namespace SceneRunner.ECSWorld
{
    /// <summary>
    ///     Global entities that can be shared between scenes
    /// </summary>
    public class GlobalEntities
    {
        private readonly Entity camera;

        /// <summary>
        ///     Not implemented yet
        /// </summary>
        private readonly Entity player;

        /// <summary>
        ///     The global world entities belong to
        /// </summary>
        private readonly World world;

        public GlobalEntities(World world, Entity player, Entity camera)
        {
            this.world = world;
            this.player = player;
            this.camera = camera;
        }

        public Camera GetCamera() =>
            world.Get<CameraComponent>(camera).Camera;
    }
}
