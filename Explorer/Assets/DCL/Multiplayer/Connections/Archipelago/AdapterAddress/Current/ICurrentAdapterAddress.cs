using ECS;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current
{
    public interface ICurrentAdapterAddress
    {
        string AdapterUrl();

        public static ICurrentAdapterAddress NewDefault(IRealmData realmData)
        {
            return new CurrentAdapterAddress(
                IAdapterAddresses.NewDefault(),
                realmData
            );
        }
    }
}
