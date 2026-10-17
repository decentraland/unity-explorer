using Arch.Core;

namespace DCL.PluginSystem.Global
{
    public readonly struct GlobalPluginArguments
    {
        /// <summary>
        ///     Player entity is persistent and never dies
        /// </summary>
        public readonly Entity PlayerEntity;
        public readonly Entity SkyboxEntity;

        public GlobalPluginArguments(Entity playerEntity, Entity skyboxEntity)
        {
            PlayerEntity = playerEntity;
            SkyboxEntity = skyboxEntity;
        }
    }
}
