using Arch.SystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.Systems;
using LiveKit.Internal.FFIClients;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;

        public MultiplayerPlugin(IArchipelagoIslandRoom archipelagoIslandRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments _)
        {
#if !NO_LIVEKIT_MODE
            IFFIClient.Default.EnsureInitialize();
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom);
#endif
        }
    }
}
