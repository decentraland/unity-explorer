using Arch.Core;

namespace SceneRunner.Mapping
{
    public interface IReadOnlySceneMapping
    {
        World? GetWorld(string sceneName);
    }

    public interface ISceneMapping : IReadOnlySceneMapping
    {
        void Register(string sceneName, World world);
    }
}
