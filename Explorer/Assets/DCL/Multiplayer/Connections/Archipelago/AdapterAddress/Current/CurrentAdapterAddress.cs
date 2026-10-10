using ECS;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current
{
    public class CurrentAdapterAddress : ICurrentAdapterAddress
    {
        private readonly IAdapterAddresses adapterAddresses;
        private readonly IRealmData currentRealmData;

        public CurrentAdapterAddress(IAdapterAddresses adapterAddresses, IRealmData currentRealmData)
        {
            this.adapterAddresses = adapterAddresses;
            this.currentRealmData = currentRealmData;
        }

        public string AdapterUrl() =>
            adapterAddresses.AdapterUrlAsync(currentRealmData.CommsAdapter);
    }
}
