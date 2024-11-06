namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Empty scenes do not participate in calculations
    /// </summary>
    public readonly struct EmptySceneComponent
    {
        public static EmptySceneComponent Create() =>
            new ();
    }
}
