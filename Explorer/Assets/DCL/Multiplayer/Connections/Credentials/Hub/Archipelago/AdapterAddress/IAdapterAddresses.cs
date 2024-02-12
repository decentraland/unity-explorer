using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress
{
    public interface IAdapterAddresses
    {
        UniTask<string> AdapterUrl(string aboutUrl);
    }
}
