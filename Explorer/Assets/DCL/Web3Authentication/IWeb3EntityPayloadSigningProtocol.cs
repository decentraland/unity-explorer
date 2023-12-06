namespace DCL.Web3Authentication
{
    public interface IWeb3EntityPayloadSigningProtocol
    {
        AuthChain Sign(string entityId);
    }
}
