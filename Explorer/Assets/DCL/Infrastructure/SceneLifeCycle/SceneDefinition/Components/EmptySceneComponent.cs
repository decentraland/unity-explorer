namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Empty scenes do not participate in calculations
    /// </summary>
    public readonly struct EmptySceneComponent
    {
        internal static EmptySceneComponent Create() =>
            new ();
    }
}
