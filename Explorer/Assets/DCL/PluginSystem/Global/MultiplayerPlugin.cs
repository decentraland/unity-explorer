using Arch.SystemGroups;
using DCL.Multiplayer.Connections.Credentials.Hub;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using LiveKit.Internal.FFIClients.Pools;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMutableRoomHub roomHub;
        private readonly IMultiPool multiPool;
        private readonly ICredentialsHub credentialsHub;

        public MultiplayerPlugin() : this(new ThreadSafeMultiPool(), ICredentialsHub.Null.INSTANCE)
        {
        }

        public MultiplayerPlugin(IMultiPool multiPool, ICredentialsHub credentialsHub) : this(
            new MutableRoomHub(multiPool),
            multiPool,
            credentialsHub
        ) { }

        public MultiplayerPlugin(IMutableRoomHub roomHub, IMultiPool multiPool, ICredentialsHub credentialsHub)
        {
            this.roomHub = roomHub;
            this.multiPool = multiPool;
            this.credentialsHub = credentialsHub;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ConnectionRoomsSystem.InjectToWorld(ref builder, roomHub, multiPool, credentialsHub);
        }
    }
}
