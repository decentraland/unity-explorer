using Arch.Core;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     The scene and scene-world entity a player's voice state was last written to.
    /// </summary>
    public struct PlayerVoiceStateTargetComponent
    {
        public ISceneFacade? Scene;
        public Entity SceneEntity;
    }
}
