using Arch.Core;
using SceneRuntime;

namespace DCL.PluginSystem.Global
{
    public readonly struct GlobalPluginArguments
    {
        /// <summary>
        ///     Player entity is persistent and never dies
        /// </summary>
        public readonly Entity PlayerEntity;

        public readonly V8ActiveEngines V8ActiveEngines;

        public GlobalPluginArguments(Entity playerEntity, V8ActiveEngines v8ActiveEngines)
        {
            PlayerEntity = playerEntity;
            V8ActiveEngines = v8ActiveEngines;
        }
    }
}
