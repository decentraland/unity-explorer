namespace SceneRunner.ECSWorld
{
    public interface IECSWorldFactory
    {
        /// <summary>
        ///     Create a new instance of the ECS world, all its systems and attach them to the player loop
        /// </summary>
        ECSWorldFacade CreateWorld(in ECSWorldFactoryArgs args);
    }
}
