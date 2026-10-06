namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public interface IAdapterAddresses
    {
        string AdapterUrlAsync(string commsAdapter);

        public static IAdapterAddresses NewDefault()
        {
            return new LogAdapterAddresses(
                new RefinedAdapterAddresses()
                );
        }
    }
}
