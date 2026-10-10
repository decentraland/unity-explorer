namespace ECS
{
    /// <summary>
    ///     Connection status of the comms room backing scenes, exposed without a dependency on the comms assemblies
    /// </summary>
    public interface ISceneRoomStatus
    {
        /// <summary>
        ///     True when the comms for the given scene are settled: the room is connected for that scene,
        ///     or it is in a state in which it will never connect (deactivated, offline realm comms, forbidden access)
        /// </summary>
        bool IsSceneRoomSettled(string sceneId);
    }
}
