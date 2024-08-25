using Arch.Core;
using DCL.AvatarRendering.Emotes;
using SceneRuntime;

namespace DCL.PluginSystem.Global
{
    public readonly struct GlobalPluginArguments
    {
        /// <summary>
        ///     Player entity is persistent and never dies
        /// </summary>
        public readonly Entity PlayerEntity;

        public readonly IEmoteProvider EmoteProvider;

        public readonly V8ActiveEngines V8ActiveEngines;

        public GlobalPluginArguments(Entity playerEntity, IEmoteProvider emoteProvider, V8ActiveEngines v8ActiveEngines)
        {
            PlayerEntity = playerEntity;
            EmoteProvider = emoteProvider;
            V8ActiveEngines = v8ActiveEngines;
        }
    }
}
