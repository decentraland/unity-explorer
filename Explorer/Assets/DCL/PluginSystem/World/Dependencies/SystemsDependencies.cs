using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.RoomHubs;

namespace DCL.PluginSystem.World.Dependencies
{
    /// <summary>
    ///     Application-scoped services consumed by scene systems inside <see cref="IDCLWorldPlugin.InjectToWorld" />.
    ///     Unlike <see cref="ECSWorldInstanceSharedDependencies" />, these are the same instances across every scene
    ///     world — they resolve the dependency tree rather than carrying per-instance scene state.
    /// </summary>
    public readonly struct SystemsDependencies
    {
        public readonly IRoomHub RoomHub;
        public readonly IEntityCollidersGlobalCache EntityCollidersGlobalCache;

        public SystemsDependencies(IRoomHub roomHub, IEntityCollidersGlobalCache entityCollidersGlobalCache)
        {
            RoomHub = roomHub;
            EntityCollidersGlobalCache = entityCollidersGlobalCache;
        }
    }
}
