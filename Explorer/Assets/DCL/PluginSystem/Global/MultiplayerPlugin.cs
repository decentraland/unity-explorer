using Arch.SystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Systems;
using LiveKit.Internal.FFIClients;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;

        public MultiplayerPlugin(IArchipelagoIslandRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments _)
        {
            IFFIClient.Default.EnsureInitialize();
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom);
        }
    }
}
