using Arch.SystemGroups;
using DCL.Character;
using DCL.Multiplayer.Connections.Credentials.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.Systems;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMultiPool multiPool;
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;

        public MultiplayerPlugin(ICharacterObject characterObject, IWeb3IdentityCache web3IdentityCache, IMultiPool multiPool) : this(
            new ArchipelagoIslandRoom(characterObject, web3IdentityCache, multiPool),
            multiPool
        ) { }

        public MultiplayerPlugin(IArchipelagoIslandRoom archipelagoIslandRoom, IMultiPool multiPool)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.multiPool = multiPool;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            IFFIClient.Default.EnsureInitialize();
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, multiPool);
        }
    }
}
