using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;

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
        public readonly IReadOnlyLoadingStatus LoadingStatus;

        public SystemsDependencies(IRoomHub roomHub, IEntityCollidersGlobalCache entityCollidersGlobalCache, IReadOnlyLoadingStatus loadingStatus)
        {
            RoomHub = roomHub;
            EntityCollidersGlobalCache = entityCollidersGlobalCache;
            LoadingStatus = loadingStatus;
        }
    }
}
