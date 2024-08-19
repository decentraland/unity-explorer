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

        public readonly V8EngineFactory V8EngineFactory;

        public GlobalPluginArguments(Entity playerEntity, IEmoteProvider emoteProvider, V8EngineFactory v8EngineFactory)
        {
            PlayerEntity = playerEntity;
            EmoteProvider = emoteProvider;
            V8EngineFactory = v8EngineFactory;
        }
    }
}
