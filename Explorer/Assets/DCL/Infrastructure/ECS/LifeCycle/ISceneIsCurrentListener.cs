namespace ECS.LifeCycle
{
    /// <summary>
    ///     Executes special logic when the scene is set as current or left.
    ///     It will be executed even if the scene has stopped with an error
    /// </summary>
    public interface ISceneIsCurrentListener
    {
        void OnSceneIsCurrentChanged(bool value);
    }
}
