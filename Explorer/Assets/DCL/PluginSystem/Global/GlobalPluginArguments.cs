using Arch.Core;

namespace DCL.PluginSystem.Global
{
    public readonly struct GlobalPluginArguments
    {
        /// <summary>
        ///     Player entity is persistent and never dies
        /// </summary>
        public readonly Entity PlayerEntity;

        public GlobalPluginArguments(Entity playerEntity)
        {
            PlayerEntity = playerEntity;
        }
    }
}
